import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, map, Observable, throwError } from 'rxjs';
import { ApiResponse, PagedResponse } from './api-response.model';
import { EXPLORE_PAGE_SIZE, ExploreCard } from './explore-api';

/** Largest photo the API accepts (API_SPEC §36). */
export const MAX_PHOTO_BYTES = 10 * 1024 * 1024;

/** Photo types the API accepts; others are refused before uploading. */
export const PHOTO_TYPES = ['image/jpeg', 'image/png', 'image/webp'] as const;

/** POST /api/identification candidate (API_SPEC §37). No confidence values exist (D-013). */
export interface IdentificationCandidate {
  readonly rank: number;
  readonly plant: {
    readonly id: string;
    readonly name: string;
    readonly plantGroup: string | null;
    /** The plant group's slug, for /plant-groups/{plantGroupId}. */
    readonly plantGroupId: string | null;
    readonly scientificName: string;
    readonly imageUrl: string | null;
  };
}

/**
 * Why identification did not produce candidates:
 * not-identified (422: nothing matched the encyclopedia), unavailable (503: provider off, busy or failing),
 * invalid-photo (400/413), rate-limited (429), offline (no response), or failed (anything else).
 */
export type IdentificationFailure =
  'not-identified' | 'unavailable' | 'invalid-photo' | 'rate-limited' | 'offline' | 'failed';

/** Client for plant identification (API_SPEC §36–42). */
@Injectable({ providedIn: 'root' })
export class IdentificationApi {
  private readonly http = inject(HttpClient);

  /**
   * Sends the photo as multipart/form-data (field `image`); HttpClient adds the CSRF header. Errors are thrown as an
   * IdentificationFailure.
   */
  identify(photo: Blob): Observable<readonly IdentificationCandidate[]> {
    const form = new FormData();
    form.append('image', photo);
    return this.http
      .post<ApiResponse<{ candidates: IdentificationCandidate[] }>>('/api/identification', form)
      .pipe(
        map((response) => response.data.candidates),
        catchError((error: unknown) => throwError(() => toFailure(error))),
      );
  }

  /**
   * GET /api/identification/related: the More page, plants related to up to three candidates, closest first, with
   * the same card shape and paging as Explore. Errors surface as HttpErrorResponse.
   */
  getRelated(
    plantIds: readonly string[],
    page: number,
  ): Observable<PagedResponse<readonly ExploreCard[]>> {
    return this.http.get<PagedResponse<readonly ExploreCard[]>>('/api/identification/related', {
      params: { plantId: [...plantIds], page, pageSize: EXPLORE_PAGE_SIZE },
    });
  }
}

function toFailure(error: unknown): IdentificationFailure {
  if (!(error instanceof HttpErrorResponse)) {
    return 'failed';
  }
  switch (error.status) {
    case 0:
      return 'offline';
    case 422:
      return 'not-identified';
    case 503:
      return 'unavailable';
    case 400:
    case 413:
      return 'invalid-photo';
    case 429:
      return 'rate-limited';
    default:
      return 'failed';
  }
}
