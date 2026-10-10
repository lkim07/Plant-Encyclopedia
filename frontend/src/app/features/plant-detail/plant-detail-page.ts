import { Location } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, combineLatest, map, Observable, of, startWith, Subject, switchMap } from 'rxjs';
import { PlantApi } from '../../core/api/plant-api';
import { PlantDetail } from '../../core/api/plant-detail.model';
import { CareCard } from './care-card';
import { formatTemperature } from './format-temperature';

type PageState =
  | { readonly status: 'loading' }
  | { readonly status: 'loaded'; readonly plant: PlantDetail }
  | { readonly status: 'not-found' }
  | { readonly status: 'error' };

interface CareCardView {
  readonly key: string;
  readonly icon: string;
  readonly label: string;
  readonly value: string;
  readonly description: string | null;
}

/** Plant Detail (UX §25–29): hero placeholder, naming hierarchy, description, Quick Care. */
@Component({
  selector: 'app-plant-detail-page',
  imports: [RouterLink, CareCard],
  templateUrl: './plant-detail-page.html',
  styleUrl: './plant-detail-page.css',
})
export class PlantDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly plantApi = inject(PlantApi);
  private readonly retry$ = new Subject<void>();

  protected readonly skeletonCards = [1, 2, 3, 4];

  /** Reloads whenever the plant ID in the URL changes or the user presses Try Again. */
  protected readonly state = toSignal(
    combineLatest([
      this.route.paramMap.pipe(map((params) => params.get('plantId') ?? '')),
      this.retry$.pipe(startWith(undefined)),
    ]).pipe(switchMap(([plantId]) => this.load(plantId))),
    { initialValue: { status: 'loading' } as PageState },
  );

  protected readonly careCards = computed<CareCardView[]>(() => {
    const state = this.state();
    const care = state.status === 'loaded' ? state.plant.quickCare : null;
    if (!care) {
      return [];
    }

    const cards: CareCardView[] = [];
    if (care.light) {
      cards.push({ key: 'light', icon: '☀', label: 'Light', ...care.light });
    }
    if (care.water) {
      cards.push({ key: 'water', icon: '💧', label: 'Water', ...care.water });
    }
    const temperature = care.temperature ? formatTemperature(care.temperature) : null;
    if (care.temperature && temperature) {
      cards.push({
        key: 'temperature',
        icon: '🌡',
        label: 'Temp',
        value: temperature,
        description: care.temperature.description,
      });
    }
    if (care.soil) {
      cards.push({ key: 'soil', icon: '🌱', label: 'Soil', ...care.soil });
    }
    return cards;
  });

  protected retry(): void {
    this.retry$.next();
  }

  /** Back follows in-app history; a page opened directly goes to the home destination. */
  protected goBack(): void {
    const navigationId = (this.location.getState() as { navigationId?: number } | null)?.navigationId;
    if (navigationId !== undefined && navigationId > 1) {
      this.location.back();
    } else {
      void this.router.navigateByUrl('/identify');
    }
  }

  private load(plantId: string): Observable<PageState> {
    return this.plantApi.getPlant(plantId).pipe(
      map((plant): PageState => ({ status: 'loaded', plant })),
      // 404: unknown or unpublished plant. 400: malformed ID. Both mean "this plant doesn't exist".
      catchError((error: unknown) =>
        of<PageState>(
          error instanceof HttpErrorResponse && (error.status === 404 || error.status === 400)
            ? { status: 'not-found' }
            : { status: 'error' },
        ),
      ),
      startWith<PageState>({ status: 'loading' }),
    );
  }
}
