import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResponse } from './api-response.model';
import { PlantGroupDetail } from './browse.model';

/** Plants per page; UX §53 suggests an initial feed of about 12–20 cards. */
export const PLANT_GROUP_PAGE_SIZE = 20;

/** Client for the plant-group endpoint (API_SPEC §19). */
@Injectable({ providedIn: 'root' })
export class PlantGroupApi {
  private readonly http = inject(HttpClient);

  /** GET /api/plant-groups/{plantGroupId}. Errors surface as HttpErrorResponse (404, 400, 500, network). */
  getPlantGroup(plantGroupId: string, page: number): Observable<PagedResponse<PlantGroupDetail>> {
    return this.http.get<PagedResponse<PlantGroupDetail>>(
      `/api/plant-groups/${encodeURIComponent(plantGroupId)}`,
      { params: { page, pageSize: PLANT_GROUP_PAGE_SIZE } },
    );
  }
}
