import { Injectable } from '@angular/core';
import { ExploreCard } from '../../core/api/explore-api';

/** The Explore feed as the user left it. */
export interface SavedFeed {
  readonly cards: readonly ExploreCard[];
  readonly page: number;
  readonly hasNext: boolean;
  readonly feedAt: string;
  readonly scrollY: number;
  /** Whose feed it is (null: signed out); a personalized feed is never shown to someone else. */
  readonly userId: string | null;
}

/**
 * Remembers the last Explore feed for this visit (in memory only), so Back returns to the same cards and scroll
 * position (UX §15). Only one feed is kept; leaving Explore replaces it.
 */
@Injectable({ providedIn: 'root' })
export class ExploreFeedMemory {
  saved: SavedFeed | null = null;
}
