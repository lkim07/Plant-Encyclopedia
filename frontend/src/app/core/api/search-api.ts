import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponse } from './api-response.model';
import { SearchResultItem, SearchSuggestion } from './search.model';

/** Results per page. */
export const SEARCH_PAGE_SIZE = 20;

/** The API stops search results at this page (API_SPEC §24). */
export const SEARCH_MAX_PAGE = 50;

/** Client for the search endpoints (API_SPEC §24–26). */
@Injectable({ providedIn: 'root' })
export class SearchApi {
  private readonly http = inject(HttpClient);

  /** GET /api/search. Errors surface as HttpErrorResponse (400, 429, 503, 500, network). */
  search(q: string, page: number): Observable<PagedResponse<readonly SearchResultItem[]>> {
    return this.http.get<PagedResponse<readonly SearchResultItem[]>>('/api/search', {
      params: { q, page, pageSize: SEARCH_PAGE_SIZE },
    });
  }

  /** GET /api/search/suggestions. */
  suggest(q: string): Observable<readonly SearchSuggestion[]> {
    return this.http
      .get<ApiResponse<readonly SearchSuggestion[]>>('/api/search/suggestions', { params: { q } })
      .pipe(map((response) => response.data));
  }
}
