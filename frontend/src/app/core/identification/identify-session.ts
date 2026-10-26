import { Injectable, signal } from '@angular/core';
import { IdentificationCandidate } from '../api/identification-api';

/** The photo the user chose, with a local address for showing it. */
export interface ChosenPhoto {
  readonly file: File;
  /** An object URL of the file; revoked when the photo is replaced or cleared. */
  readonly url: string;
  /** Where it came from, so Retake reopens the same chooser. */
  readonly source: 'camera' | 'library';
}

/**
 * The current identification, shared by Identify and Identification Results (D-014): the chosen photo and, once
 * identified, its candidates. Kept in memory for this visit only, so Back from a candidate returns to the same
 * results and Back from the results shows the same photo. The photo never leaves the browser except in the
 * identification request.
 */
@Injectable({ providedIn: 'root' })
export class IdentifySession {
  private readonly current = signal<ChosenPhoto | null>(null);
  private readonly found = signal<readonly IdentificationCandidate[] | null>(null);

  readonly photo = this.current.asReadonly();
  /** Candidates for the current photo, or null before (or without) a successful identification. */
  readonly candidates = this.found.asReadonly();

  choose(file: File, source: ChosenPhoto['source']): void {
    this.release();
    this.current.set({ file, url: URL.createObjectURL(file), source });
    this.found.set(null);
  }

  identified(candidates: readonly IdentificationCandidate[]): void {
    this.found.set(candidates);
  }

  clear(): void {
    this.release();
    this.current.set(null);
    this.found.set(null);
  }

  private release(): void {
    const photo = this.current();
    if (photo) {
      URL.revokeObjectURL(photo.url);
    }
  }
}
