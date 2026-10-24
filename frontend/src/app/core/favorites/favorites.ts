import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiErrorResponse, ApiResponse } from '../api/api-response.model';
import { Auth, toAccountError } from '../auth/auth';
import { Snackbar } from '../../shared/snackbar/snackbar';

/** GET /api/favorites item (API_SPEC §31). */
export interface Favorite {
  readonly favoriteId: string;
  readonly order: number;
  readonly plant: {
    readonly id: string;
    readonly name: string;
    readonly plantGroup: string | null;
    readonly imageUrl: string | null;
  };
}

/** The plant a bookmark belongs to. */
export interface PlantRef {
  readonly id: string;
  readonly name: string;
}

export type FavoritesLoadState = 'idle' | 'loading' | 'loaded' | 'error';

/** A saved plant in the user's order; `favorite` is null while a new save is on its way. */
interface Entry {
  readonly plantId: string;
  readonly favorite: Favorite | null;
}

const PENDING_KEY = 'plant-encyclopedia.pending-favorite';
/** A save asked for before signing in is kept this long (this browser tab only). */
export const PENDING_FAVORITE_MAX_AGE_MS = 30 * 60 * 1000;

/**
 * The signed-in user's Favorites (API_SPEC §31–34, PRD §18–19). Loaded once after sign-in so every bookmark shows
 * whether its plant is saved; cleared on sign-out. A bookmark changes at once, and the request follows; requests
 * for one plant run one after another, so quick clicks reach the server in order. If a request fails, the message
 * says why and the list is reloaded from the server.
 * Signed out, a bookmark opens the "Save this plant" prompt; the plant is remembered for this tab (sessionStorage)
 * and saved once the user has signed in, also after the Google or GitHub round trip.
 */
@Injectable({ providedIn: 'root' })
export class Favorites {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(Auth);
  private readonly snackbar = inject(Snackbar);

  private readonly entries = signal<readonly Entry[]>([]);
  private readonly state = signal<FavoritesLoadState>('idle');
  /** Favorite IDs the server has confirmed, by plant; independent of what is on screen. */
  private readonly serverIds = new Map<string, string>();
  private readonly queues = new Map<string, Promise<void>>();
  private readonly savedIds = computed(() => new Set(this.entries().map((e) => e.plantId)));
  /** The signed-in user's ID: undefined while unknown, null when signed out. */
  private readonly userId = computed(() => {
    const user = this.auth.user();
    return user === undefined ? undefined : (user?.id ?? null);
  });

  readonly loadState = this.state.asReadonly();
  /** Saved plants with their card data, in the user's order. */
  readonly saved = computed(() =>
    this.entries().flatMap((entry) => (entry.favorite ? [entry.favorite] : [])),
  );
  /** The plant whose bookmark a signed-out user selected, while the sign-in prompt is open. */
  readonly prompt = signal<PlantRef | null>(null);

  constructor() {
    effect(() => {
      const userId = this.userId();
      untracked(() => {
        if (userId) {
          void this.start();
        } else if (userId === null) {
          this.clear();
        }
      });
    });
  }

  isSaved(plantId: string): boolean {
    return this.savedIds().has(plantId);
  }

  /** Saves or removes a plant. With `undo`, removing offers "Removed from Favorites. Undo" (Favorites page). */
  toggle(plant: PlantRef, options: { undo?: boolean } = {}): void {
    const user = this.auth.user();
    if (user === undefined) {
      return; // Still checking who is signed in; the check takes a moment.
    }
    if (user === null) {
      this.prompt.set(plant);
      return;
    }
    if (this.isSaved(plant.id)) {
      this.remove(plant.id, options.undo ?? false);
    } else {
      this.save(plant.id);
    }
  }

  /** "Sign in" in the prompt: remember the plant until the user has signed in. */
  rememberPending(plantId: string): void {
    try {
      sessionStorage.setItem(PENDING_KEY, JSON.stringify({ plantId, at: Date.now() }));
    } catch {
      // Storage blocked: the user can select the bookmark again after signing in.
    }
  }

  /** Fetches the list again. The current list stays on screen while it loads. */
  async reload(): Promise<void> {
    if (this.state() !== 'loaded') {
      this.state.set('loading');
    }
    try {
      const response = await firstValueFrom(
        this.http.get<ApiResponse<readonly Favorite[]>>('/api/favorites'),
      );
      this.serverIds.clear();
      for (const favorite of response.data) {
        this.serverIds.set(favorite.plant.id, favorite.favoriteId);
      }
      this.entries.set(response.data.map((favorite) => ({ plantId: favorite.plant.id, favorite })));
      this.state.set('loaded');
    } catch {
      if (this.state() !== 'loaded') {
        this.state.set('error');
      }
    }
  }

  private async start(): Promise<void> {
    await this.reload();
    const pending = takePending();
    if (pending && !this.isSaved(pending)) {
      this.save(pending);
    }
  }

  private clear(): void {
    this.entries.set([]);
    this.serverIds.clear();
    this.state.set('idle');
  }

  /** Adds the plant at the end, or at `order` with its old card (Undo), then saves it. */
  private save(plantId: string, order?: number, restore?: Favorite): void {
    this.entries.update((entries) => {
      const entry: Entry = { plantId, favorite: restore ?? null };
      const index = order ? Math.min(order - 1, entries.length) : entries.length;
      return [...entries.slice(0, index), entry, ...entries.slice(index)];
    });

    this.enqueue(plantId, async () => {
      try {
        const response = await firstValueFrom(
          this.http.post<ApiResponse<Favorite> | null>(
            '/api/favorites',
            order ? { plantId, order } : { plantId },
          ),
        );
        const favorite = response?.data;
        if (favorite) {
          this.serverIds.set(plantId, favorite.favoriteId);
          this.entries.update((entries) =>
            entries.map((entry) => (entry.plantId === plantId ? { plantId, favorite } : entry)),
          );
        }
      } catch (error) {
        this.fail(error);
      }
    });
  }

  private remove(plantId: string, undo: boolean): void {
    const entries = this.entries();
    const index = entries.findIndex((entry) => entry.plantId === plantId);
    const removed = entries[index];
    this.entries.set(entries.filter((entry) => entry.plantId !== plantId));

    if (undo && removed?.favorite) {
      const favorite = removed.favorite;
      this.snackbar.show('Removed from Favorites.', {
        label: 'Undo',
        run: () => {
          if (!this.isSaved(plantId)) {
            this.save(plantId, index + 1, favorite);
          }
        },
      });
    }

    this.enqueue(plantId, async () => {
      const favoriteId = this.serverIds.get(plantId);
      if (!favoriteId) {
        return; // The save before it failed, so there is nothing to remove.
      }
      try {
        await firstValueFrom(this.http.delete(`/api/favorites/${favoriteId}`));
        this.serverIds.delete(plantId);
      } catch (error) {
        if (error instanceof HttpErrorResponse && error.status === 404) {
          this.serverIds.delete(plantId); // Already gone.
          return;
        }
        this.fail(error);
      }
    });
  }

  private enqueue(plantId: string, work: () => Promise<void>): void {
    const previous = this.queues.get(plantId);
    const next = previous ? previous.then(work) : work();
    this.queues.set(plantId, next);
    void next.finally(() => {
      if (this.queues.get(plantId) === next) {
        this.queues.delete(plantId);
      }
    });
  }

  private fail(error: unknown): void {
    const body =
      error instanceof HttpErrorResponse ? (error.error as ApiErrorResponse | null) : null;
    if (body?.error?.code === 'EMAIL_NOT_CONFIRMED') {
      this.snackbar.show('Confirm your email address to save plants.', {
        label: 'Resend email',
        run: () =>
          this.auth.resendConfirmation().subscribe({
            next: () => this.snackbar.show('We sent a new confirmation email.'),
            error: (resendError: unknown) =>
              this.snackbar.show(toAccountError(resendError).message),
          }),
      });
    } else {
      this.snackbar.show(toAccountError(error).message);
    }
    void this.reload();
  }
}

/** The plant remembered before sign-in, if recent; it is used once. Stored data is treated as untrusted. */
function takePending(): string | null {
  try {
    const raw = sessionStorage.getItem(PENDING_KEY);
    sessionStorage.removeItem(PENDING_KEY);
    const pending: unknown = JSON.parse(raw ?? 'null');
    if (
      pending &&
      typeof pending === 'object' &&
      'plantId' in pending &&
      'at' in pending &&
      typeof pending.plantId === 'string' &&
      /^[0-9a-f-]{36}$/i.test(pending.plantId) &&
      typeof pending.at === 'number' &&
      Date.now() - pending.at < PENDING_FAVORITE_MAX_AGE_MS
    ) {
      return pending.plantId;
    }
  } catch {
    // Storage blocked or damaged: nothing to save.
  }
  return null;
}
