import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { PlantApi } from '../../core/api/plant-api';
import {
  Retailer,
  RetailerApi,
  RetailerAttribution,
  RetailerPlace,
  RetailerSort,
  RetailerType,
} from '../../core/api/retailer-api';
import {
  DEFAULT_LOCATION,
  ShoppingLocation,
  ShoppingLocationService,
} from '../../core/location/shopping-location';
import { Icon } from '../../shared/icons/icon';

type PlantState =
  | { readonly status: 'loading' }
  | { readonly status: 'loaded'; readonly name: string }
  | { readonly status: 'not-found' }
  | { readonly status: 'error' };

type ShopsState =
  | { readonly status: 'locating' }
  | { readonly status: 'loading' }
  | { readonly status: 'error' }
  | {
      readonly status: 'loaded';
      readonly shops: readonly Retailer[];
      readonly page: number;
      readonly hasNext: boolean;
      readonly more: 'idle' | 'loading' | 'error';
      readonly attribution: RetailerAttribution;
    };

const TYPE_LABELS: Record<RetailerType, string> = {
  nursery: 'Nursery',
  gardenCentre: 'Garden centre',
  flowerShop: 'Flower shop',
};

/** Search radii offered when nothing is found, in km (the API allows up to 100). */
const RADII = [25, 50, 100] as const;

/**
 * Where to Buy (PRD §27–28, UX §33–35, D-031–D-035, D-073): shops near a location that may sell the plant. The
 * browser's location is asked for only here; without it the page uses Vancouver, BC and says so, and a town can be
 * chosen by hand. Sorted by distance (default) or relevance. Stock is never claimed (D-033): the page says shops are
 * places to check. Shop data is credited to OpenStreetMap. Online shops are not listed yet.
 */
@Component({
  selector: 'app-where-to-buy-page',
  imports: [RouterLink, Icon],
  templateUrl: './where-to-buy-page.html',
  styleUrl: './where-to-buy-page.css',
})
export class WhereToBuyPage {
  private readonly retailerApi = inject(RetailerApi);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly locations = inject(ShoppingLocationService);
  protected readonly plantId = inject(ActivatedRoute).snapshot.paramMap.get('plantId') ?? '';
  private request?: Subscription;

  protected readonly plant = signal<PlantState>({ status: 'loading' });
  protected readonly shops = signal<ShopsState>({ status: 'locating' });
  protected readonly places = signal<readonly RetailerPlace[]>([]);
  protected readonly sort = signal<RetailerSort>('distance');
  protected readonly radius = signal<number>(RADII[0]);

  /** The value of the location chooser that matches the current location. */
  protected readonly chosen = computed(() => {
    const location = this.locations.location();
    return !location || location.source === 'default'
      ? 'default'
      : location.source === 'device'
        ? 'device'
        : `place:${location.label}`;
  });

  protected readonly nextRadius = computed(() => RADII.find((r) => r > this.radius()) ?? null);

  constructor() {
    inject(PlantApi)
      .getPlant(this.plantId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (plant) => this.plant.set({ status: 'loaded', name: plant.name }),
        error: (error: unknown) =>
          this.plant.set(
            error instanceof HttpErrorResponse && (error.status === 404 || error.status === 400)
              ? { status: 'not-found' }
              : { status: 'error' },
          ),
      });
    this.retailerApi
      .getPlaces()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: (places) => this.places.set(places), error: () => this.places.set([]) });

    void this.locations.resolve().then(() => this.load());
  }

  protected typeLabel(type: RetailerType): string {
    return TYPE_LABELS[type];
  }

  protected place(retailer: Retailer): string {
    return [retailer.location.address, retailer.location.city].filter(Boolean).join(', ');
  }

  protected pickLocation(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    if (value === 'device') {
      this.shops.set({ status: 'locating' });
      void this.locations.useDevice().then(() => this.load());
      return;
    }

    const place = this.places().find((p) => `place:${p.name}` === value);
    const location: ShoppingLocation = place
      ? { label: place.name, latitude: place.latitude, longitude: place.longitude, source: 'place' }
      : DEFAULT_LOCATION;
    this.locations.choose(location);
    this.radius.set(RADII[0]);
    this.load();
  }

  protected setSort(sort: RetailerSort): void {
    if (sort !== this.sort()) {
      this.sort.set(sort);
      this.load();
    }
  }

  protected widen(): void {
    const next = this.nextRadius();
    if (next) {
      this.radius.set(next);
      this.load();
    }
  }

  /** The first page for the current location, sort and radius; also Try Again. */
  protected load(): void {
    const location = this.locations.location();
    if (!location) {
      return;
    }

    this.request?.unsubscribe();
    this.shops.set({ status: 'loading' });
    this.request = this.retailerApi
      .getRetailers(
        this.plantId,
        location.latitude,
        location.longitude,
        this.sort(),
        1,
        this.radius(),
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) =>
          this.shops.set({
            status: 'loaded',
            shops: response.data,
            page: 1,
            hasNext: response.pagination.hasNext,
            more: 'idle',
            attribution: response.attribution,
          }),
        error: () => this.shops.set({ status: 'error' }),
      });
  }

  protected loadMore(): void {
    const state = this.shops();
    const location = this.locations.location();
    if (state.status !== 'loaded' || !location || !state.hasNext || state.more === 'loading') {
      return;
    }

    this.shops.set({ ...state, more: 'loading' });
    this.request = this.retailerApi
      .getRetailers(
        this.plantId,
        location.latitude,
        location.longitude,
        this.sort(),
        state.page + 1,
        this.radius(),
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          const shown = new Set(state.shops.map((shop) => shop.id));
          this.shops.set({
            ...state,
            shops: [...state.shops, ...response.data.filter((shop) => !shown.has(shop.id))],
            page: state.page + 1,
            hasNext: response.pagination.hasNext,
            more: 'idle',
          });
        },
        error: () => this.shops.set({ ...state, more: 'error' }),
      });
  }
}
