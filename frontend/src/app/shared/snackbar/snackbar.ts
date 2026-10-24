import { Component, inject, Injectable, signal } from '@angular/core';
import { Icon } from '../icons/icon';

export interface SnackbarAction {
  readonly label: string;
  readonly run: () => void;
}

export interface SnackbarMessage {
  readonly id: number;
  readonly text: string;
  readonly action: SnackbarAction | null;
}

/** How long a message stays, unless the pointer or keyboard focus is on it. */
export const SNACKBAR_DURATION_MS = 6000;

/** One short message at a time at the bottom of the screen (UX §38), such as "Removed from Favorites. Undo". */
@Injectable({ providedIn: 'root' })
export class Snackbar {
  private readonly current = signal<SnackbarMessage | null>(null);
  private timer: ReturnType<typeof setTimeout> | undefined;
  private nextId = 0;

  readonly message = this.current.asReadonly();

  show(text: string, action: SnackbarAction | null = null): void {
    this.current.set({ id: ++this.nextId, text, action });
    this.resume();
  }

  dismiss(): void {
    this.pause();
    this.current.set(null);
  }

  /** Keeps the message while someone is reading it or moving to its action. */
  pause(): void {
    clearTimeout(this.timer);
  }

  resume(): void {
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.current.set(null), SNACKBAR_DURATION_MS);
  }
}

/**
 * Shows the Snackbar's message. The live region is always present, so screen readers announce each new message
 * politely; the action and the close button are ordinary buttons reachable with the keyboard.
 */
@Component({
  selector: 'app-snackbar',
  imports: [Icon],
  template: `
    <div class="region" role="status" aria-live="polite">
      @if (snackbar.message(); as message) {
        <div
          class="snackbar"
          (mouseenter)="snackbar.pause()"
          (mouseleave)="snackbar.resume()"
          (focusin)="snackbar.pause()"
          (focusout)="snackbar.resume()"
        >
          <span class="text">{{ message.text }}</span>
          @if (message.action; as action) {
            <button type="button" class="action" (click)="act(action)">{{ action.label }}</button>
          }
          <button
            type="button"
            class="icon-button close"
            aria-label="Dismiss message"
            (click)="snackbar.dismiss()"
          >
            <app-icon name="close" />
          </button>
        </div>
      }
    </div>
  `,
  styles: `
    .region {
      position: fixed;
      right: var(--space-4);
      bottom: calc(var(--space-4) + env(safe-area-inset-bottom, 0px));
      left: var(--space-4);
      z-index: 20;
      display: flex;
      justify-content: center;
      pointer-events: none;
    }

    .snackbar {
      display: flex;
      align-items: center;
      gap: var(--space-2);
      max-width: 32rem;
      padding: var(--space-1) var(--space-1) var(--space-1) var(--space-4);
      border-radius: var(--radius-md);
      background: var(--color-text);
      color: var(--color-bg);
      box-shadow: 0 6px 20px rgb(40 28 20 / 0.25);
      pointer-events: auto;
    }

    .text {
      flex: 1;
      padding: var(--space-2) 0;
    }

    .action {
      min-height: var(--touch-target);
      padding: 0 var(--space-3);
      border: none;
      border-radius: var(--radius-sm);
      background: transparent;
      color: var(--color-surface-strong);
      font-weight: 700;
      text-decoration: underline;
      cursor: pointer;
    }

    .close {
      --icon-color: var(--color-bg);
    }

    @media (prefers-reduced-motion: no-preference) {
      .snackbar {
        animation: rise 200ms var(--ease-out-soft);
      }
    }

    @keyframes rise {
      from {
        opacity: 0;
        transform: translateY(0.75rem);
      }
    }
  `,
})
export class SnackbarHost {
  protected readonly snackbar = inject(Snackbar);

  protected act(action: SnackbarAction): void {
    this.snackbar.dismiss();
    action.run();
  }
}
