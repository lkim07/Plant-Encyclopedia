import { HttpClient } from '@angular/common/http';
import { computed, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiResponse } from '../api/api-response.model';
import { Auth } from '../auth/auth';

const STORAGE_KEY = 'plant-encyclopedia.recent-searches';
export const MAX_RECENT_SEARCHES = 10;
const MAX_TEXT_LENGTH = 100;

/** GET /api/history/searches item (API_SPEC §28). */
interface SearchHistoryItem {
  readonly searchId: string;
  readonly query: string;
}

/**
 * Recent searches (UX §20). Signed in, they are the user's search history on the server (API_SPEC §28): loaded
 * after sign-in, changed at once on screen, with requests sent one after another so a removal always follows the
 * save it undoes; if a request fails, the list is loaded again. Nothing is written to this browser then.
 * Signed out (or while the check is still running), they are kept only in this browser (localStorage). Stored data
 * is treated as untrusted: anything that is not a short list of short strings is ignored. If storage is
 * unavailable (private mode, blocked), the list still works for this visit.
 */
@Injectable({ providedIn: 'root' })
export class RecentSearches {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(Auth);

  private readonly local = signal<readonly string[]>(read());
  private readonly server = signal<readonly string[]>([]);
  /** Server IDs by lower-case text, kept even after an entry leaves the screen so a removal can follow its save. */
  private readonly serverIds = new Map<string, string>();
  private queue: Promise<void> = Promise.resolve();
  private readonly userId = computed(() => this.auth.user()?.id ?? null);

  /** Newest first. */
  readonly items = computed(() => (this.userId() ? this.server() : this.local()));

  constructor() {
    effect(() => {
      const userId = this.userId();
      untracked(() => {
        this.server.set([]);
        this.serverIds.clear();
        if (userId) {
          this.enqueue(() => this.load());
        }
      });
    });
  }

  /** Adds a submitted search at the top; an existing entry (ignoring case) moves up instead of repeating. */
  add(text: string): void {
    const value = text.trim();
    if (!value || value.length > MAX_TEXT_LENGTH) {
      return;
    }

    const key = value.toLocaleLowerCase();
    const next = (items: readonly string[]) =>
      [value, ...items.filter((item) => item.toLocaleLowerCase() !== key)].slice(
        0,
        MAX_RECENT_SEARCHES,
      );

    if (!this.userId()) {
      this.saveLocal(next(this.local()));
      return;
    }

    this.server.set(next(this.server()));
    this.enqueue(async () => {
      const response = await firstValueFrom(
        this.http.post<ApiResponse<SearchHistoryItem>>('/api/history/searches', { query: value }),
      );
      this.serverIds.set(key, response.data.searchId);
    });
  }

  remove(text: string): void {
    if (!this.userId()) {
      this.saveLocal(this.local().filter((item) => item !== text));
      return;
    }

    const key = text.toLocaleLowerCase();
    this.server.set(this.server().filter((item) => item !== text));
    this.enqueue(async () => {
      const searchId = this.serverIds.get(key);
      if (searchId) {
        this.serverIds.delete(key);
        await firstValueFrom(this.http.delete(`/api/history/searches/${searchId}`));
      }
    });
  }

  private async load(): Promise<void> {
    const response = await firstValueFrom(
      this.http.get<ApiResponse<SearchHistoryItem[]>>('/api/history/searches'),
    );
    this.serverIds.clear();
    for (const item of response.data) {
      this.serverIds.set(item.query.toLocaleLowerCase(), item.searchId);
    }
    this.server.set(response.data.map((item) => item.query).slice(0, MAX_RECENT_SEARCHES));
  }

  /** Runs after the requests before it. A failure reloads the list (once) so the screen matches the server. */
  private enqueue(work: () => Promise<void>): void {
    const userId = this.userId();
    this.queue = this.queue.then(async () => {
      if (this.userId() !== userId) {
        return; // Signed out or switched users meanwhile.
      }
      try {
        await work();
      } catch {
        if (this.userId() === userId) {
          await this.load().catch(() => this.server.set([]));
        }
      }
    });
  }

  private saveLocal(items: readonly string[]): void {
    this.local.set(items);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(items));
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
