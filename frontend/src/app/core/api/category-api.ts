import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse } from './api-response.model';
import { CategoryDetail, CategorySummary } from './browse.model';

/** Client for the category endpoints (API_SPEC §17–18). */
@Injectable({ providedIn: 'root' })
export class CategoryApi {
  private readonly http = inject(HttpClient);

  /** GET /api/categories, ordered by name. */
  getCategories(): Observable<readonly CategorySummary[]> {
    return this.http
      .get<ApiResponse<readonly CategorySummary[]>>('/api/categories')
      .pipe(map((response) => response.data));
  }

  /** GET /api/categories/{categoryId}. Errors surface as HttpErrorResponse (404, 500, network). */
  getCategory(categoryId: string): Observable<CategoryDetail> {
    return this.http
      .get<ApiResponse<CategoryDetail>>(`/api/categories/${encodeURIComponent(categoryId)}`)
      .pipe(map((response) => response.data));
  }
}
