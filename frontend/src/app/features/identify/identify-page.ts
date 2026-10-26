import { Component, DestroyRef, ElementRef, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import {
  IdentificationApi,
  IdentificationFailure,
  MAX_PHOTO_BYTES,
  PHOTO_TYPES,
} from '../../core/api/identification-api';
import { ChosenPhoto, IdentifySession } from '../../core/identification/identify-session';
import { Icon } from '../../shared/icons/icon';

type Step = 'ready' | 'identifying' | { readonly failure: IdentificationFailure };

/** What each failure says and offers (UX §44: recoverable, never "this plant does not exist"). */
const FAILURES: Record<
  IdentificationFailure,
  { readonly title: string; readonly text: string; readonly retry: boolean }
> = {
  'not-identified': {
    title: "We couldn't identify this plant",
    text: 'The image may be unclear, or the plant may not be in our database yet.',
    retry: true,
  },
  unavailable: {
    title: "Identification isn't available right now",
    text: 'Please try again in a little while.',
    retry: true,
  },
  'rate-limited': {
    title: 'Too many tries in a short time',
    text: 'Please wait a minute and try again.',
    retry: true,
  },
  offline: {
    title: "We couldn't reach the server",
    text: 'Please check your connection and try again.',
    retry: true,
  },
  'invalid-photo': {
    title: "This photo can't be used",
    text: 'Choose a JPEG, PNG or WebP photo of up to 10 MB.',
    retry: false,
  },
  failed: {
    title: 'Something went wrong',
    text: 'Please try again.',
    retry: true,
  },
};

/**
 * Identify, the home screen (UX §6–9, §44; D-014, D-046): Take a Photo or Choose from Photos, a preview with Retake
 * and Identify, then "Identifying your plant…" over the same photo, and on success the Identification Results page.
 * Failures keep the photo and offer Try Again and Choose Another Photo. The photo is only read locally and sent in
 * the identification request; nothing else is done with it.
 */
@Component({
  selector: 'app-identify-page',
  imports: [Icon],
  templateUrl: './identify-page.html',
  styleUrl: './identify-page.css',
})
export class IdentifyPage {
  private readonly api = inject(IdentificationApi);
  private readonly router = inject(Router);
  protected readonly session = inject(IdentifySession);

  private readonly cameraInput = viewChild.required<ElementRef<HTMLInputElement>>('camera');
  private readonly libraryInput = viewChild.required<ElementRef<HTMLInputElement>>('library');
  private request?: Subscription;

  protected readonly accept = PHOTO_TYPES.join(',');
  protected readonly step = signal<Step>('ready');
  /** Why a chosen file was not accepted (shown instead of a preview). */
  protected readonly choiceError = signal<string | null>(null);
  protected readonly failures = FAILURES;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.request?.unsubscribe());
  }

  protected open(source: ChosenPhoto['source']): void {
    (source === 'camera' ? this.cameraInput() : this.libraryInput()).nativeElement.click();
  }

  /** Retake and Choose Another Photo: the chooser the current photo came from (the library if none). */
  protected chooseAgain(): void {
    this.open(this.session.photo()?.source ?? 'library');
  }

  protected chosen(event: Event, source: ChosenPhoto['source']): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // choosing the same file again still counts as a choice
    if (!file) {
      return; // the chooser was closed without a photo
    }

    // The API checks again; these only spare a pointless upload. An empty type (some systems) is left to the API.
    if (file.type && !(PHOTO_TYPES as readonly string[]).includes(file.type)) {
      this.choiceError.set('Choose a JPEG, PNG or WebP photo.');
      return;
    }
    if (file.size > MAX_PHOTO_BYTES) {
      this.choiceError.set('Choose a photo of up to 10 MB.');
      return;
    }

    this.choiceError.set(null);
    this.request?.unsubscribe();
    this.step.set('ready');
    this.session.choose(file, source);
  }

  /** Identify, and Try Again after a failure. */
  protected identify(): void {
    const photo = this.session.photo();
    if (!photo || this.step() === 'identifying') {
      return;
    }

    this.step.set('identifying');
    this.request = this.api.identify(photo.file).subscribe({
      next: (candidates) => {
        this.session.identified(candidates);
        this.step.set('ready');
        void this.router.navigateByUrl('/identify/results');
      },
      error: (failure: IdentificationFailure) => this.step.set({ failure }),
    });
  }

  protected failureOf(step: Step): IdentificationFailure | null {
    return typeof step === 'object' ? step.failure : null;
  }
}
