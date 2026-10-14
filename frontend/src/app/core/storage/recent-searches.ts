import { Injectable, signal } from '@angular/core';

const STORAGE_KEY = 'plant-encyclopedia.recent-searches';
export const MAX_RECENT_SEARCHES = 10;
const MAX_TEXT_LENGTH = 100;

/**
 * Recent searches kept only in this browser (localStorage) until server-side search history exists
 * (Phase 8). Stored data is treated as untrusted: anything that is not a short list of short strings
 * is ignored. If storage is unavailable (private mode, blocked), the list still works for this visit.
 */
@Injectable({ providedIn: 'root' })
export class RecentSearches {
  private readonly list = signal<readonly string[]>(read());

  /** Newest first. */
  readonly items = this.list.asReadonly();

  /** Adds a submitted search at the top; an existing entry (ignoring case) moves up instead of repeating. */
  add(text: string): void {
    const value = text.trim();
    if (!value || value.length > MAX_TEXT_LENGTH) {
      return;
    }

    const key = value.toLocaleLowerCase();
    this.save([value, ...this.list().filter((item) => item.toLocaleLowerCase() !== key)]);
  }

  remove(text: string): void {
    this.save(this.list().filter((item) => item !== text));
  }

  private save(items: readonly string[]): void {
    const next = items.slice(0, MAX_RECENT_SEARCHES);
    this.list.set(next);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    } catch {
      // Storage full or blocked: keep the in-memory list for this visit.
    }
  }
}

function read(): readonly string[] {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '[]');
    if (!Array.isArray(parsed)) {
      return [];
    }

    const seen = new Set<string>();
    const items: string[] = [];
    for (const item of parsed) {
      if (typeof item !== 'string') {
        continue;
      }
      const value = item.trim();
      const key = value.toLocaleLowerCase();
      if (value && value.length <= MAX_TEXT_LENGTH && !seen.has(key)) {
        seen.add(key);
        items.push(value);
      }
    }
    return items.slice(0, MAX_RECENT_SEARCHES);
  } catch {
    // Malformed JSON or blocked storage.
    return [];
  }
}
