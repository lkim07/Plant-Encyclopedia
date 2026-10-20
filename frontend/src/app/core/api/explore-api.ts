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

/** Client for the Explore feed (API_SPEC §20–23). */
@Injectable({ providedIn: 'root' })
export class ExploreApi {
  private readonly http = inject(HttpClient);

  /** GET /api/explore. Errors surface as HttpErrorResponse (400, 500, network). */
  getFeed(page: number): Observable<PagedResponse<readonly ExploreCard[]>> {
    return this.http.get<PagedResponse<readonly ExploreCard[]>>('/api/explore', {
      params: { page, pageSize: EXPLORE_PAGE_SIZE },
    });
  }
}
