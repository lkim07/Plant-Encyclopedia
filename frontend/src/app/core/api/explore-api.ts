import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResponse } from './api-response.model';

/** Cards per page. */
export const EXPLORE_PAGE_SIZE = 20;

/** GET /api/explore item (API_SPEC §20). */
export interface ExploreCard {
  readonly id: string;
  readonly name: string;
  readonly plantGroup: string | null;
  /** The card image (a photo, else an illustration), or null. */
  readonly imageUrl: string | null;
}

/** GET /api/explore response: a page of cards, plus the moment the feed was composed (API_SPEC §20). */
export type ExploreFeed = PagedResponse<readonly ExploreCard[]> & { readonly feedAt: string };

/** Client for the Explore feed (API_SPEC §20–23). */
@Injectable({ providedIn: 'root' })
export class ExploreApi {
  private readonly http = inject(HttpClient);

  /**
   * GET /api/explore. Later pages pass the first page's `feedAt`, so a signed-in user's personalized order stays
   * the same while they scroll. Errors surface as HttpErrorResponse (400, 500, network).
   */
  getFeed(page: number, feedAt?: string): Observable<ExploreFeed> {
    return this.http.get<ExploreFeed>('/api/explore', {
      params: { page, pageSize: EXPLORE_PAGE_SIZE, ...(feedAt ? { feedAt } : {}) },
    });
  }
}
