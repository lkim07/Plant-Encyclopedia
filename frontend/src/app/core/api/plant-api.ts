import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse } from './api-response.model';
import { PlantDetail } from './plant-detail.model';

/** Client for the plant endpoints (API_SPEC §14). */
@Injectable({ providedIn: 'root' })
export class PlantApi {
  private readonly http = inject(HttpClient);

  /** GET /api/plants/{plantId}. Errors surface as HttpErrorResponse (404, 400, 500, network). */
  getPlant(plantId: string): Observable<PlantDetail> {
    return this.http
      .get<ApiResponse<PlantDetail>>(`/api/plants/${encodeURIComponent(plantId)}`)
      .pipe(map((response) => response.data));
  }
}
