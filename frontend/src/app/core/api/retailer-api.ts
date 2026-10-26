import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, Pagination } from './api-response.model';

/** Shops per page. */
export const RETAILER_PAGE_SIZE = 20;

export type RetailerType = 'nursery' | 'gardenCentre' | 'flowerShop';
export type RetailerSort = 'distance' | 'relevance';

/** GET /api/plants/{plantId}/retailers item (API_SPEC §47). Availability is always unknown (D-033). */
export interface Retailer {
  readonly id: string;
  readonly name: string;
  readonly type: RetailerType;
  readonly location: {
    readonly address: string | null;
    readonly city: string | null;
    readonly region: string | null;
    readonly distanceKm: number;
  };
  readonly rating: number | null;
  readonly website: string | null;
  readonly availability: { readonly status: 'unknown' | 'available' | 'unavailable' };
}

/** The credit shown with the shops (OpenStreetMap, ODbL). */
export interface RetailerAttribution {
  readonly name: string;
  readonly notice: string;
  readonly license: string;
  readonly url: string;
  readonly retrievedAt: string | null;
}

export interface RetailersPage {
  readonly data: readonly Retailer[];
  readonly pagination: Pagination;
  readonly attribution: RetailerAttribution;
}

/** GET /api/retailers/places item: a town and the average position of its shops. */
export interface RetailerPlace {
  readonly name: string;
  readonly region: string | null;
  readonly latitude: number;
  readonly longitude: number;
}

/** Client for Where to Buy (API_SPEC §45–51). Errors surface as HttpErrorResponse. */
@Injectable({ providedIn: 'root' })
export class RetailerApi {
  private readonly http = inject(HttpClient);

  getRetailers(
    plantId: string,
    latitude: number,
    longitude: number,
    sort: RetailerSort,
    page: number,
    radiusKm?: number,
  ): Observable<RetailersPage> {
    return this.http.get<RetailersPage>(`/api/plants/${encodeURIComponent(plantId)}/retailers`, {
      params: {
        latitude,
        longitude,
        sort,
        page,
        pageSize: RETAILER_PAGE_SIZE,
        ...(radiusKm ? { radius: radiusKm } : {}),
      },
    });
  }

  getPlaces(): Observable<readonly RetailerPlace[]> {
    return this.http
      .get<ApiResponse<RetailerPlace[]>>('/api/retailers/places')
      .pipe(map((response) => response.data));
  }
}
