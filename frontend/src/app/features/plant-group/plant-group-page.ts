import { Location } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  catchError,
  combineLatest,
  map,
  Observable,
  of,
  startWith,
  Subject,
  Subscription,
  switchMap,
  tap,
} from 'rxjs';
import { PagedResponse } from '../../core/api/api-response.model';
import { PlantCard, PlantGroupDetail } from '../../core/api/browse.model';
import { PlantGroupApi } from '../../core/api/plant-group-api';
import { goBack } from '../../core/navigation/go-back';
import { ImageCard } from '../../shared/image-card/image-card';
import { Icon } from '../../shared/icons/icon';

interface LoadedState {
  readonly status: 'loaded';
  readonly group: PlantGroupDetail;
  /** Every plant loaded so far, across pages. */
  readonly plants: readonly PlantCard[];
  readonly page: number;
  readonly hasNext: boolean;
  readonly more: 'idle' | 'loading' | 'error';
}

type PageState =
  | { readonly status: 'loading' }
  | LoadedState
  | { readonly status: 'not-found' }
  | { readonly status: 'error' };

/** Plant Group page (UX §23): published plants of one group, loaded progressively (UX §53). */
@Component({
  selector: 'app-plant-group-page',
  imports: [RouterLink, ImageCard, Icon],
  templateUrl: './plant-group-page.html',
  styleUrls: ['../../shared/browse-page.css', './plant-group-page.css'],
})
export class PlantGroupPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly destroyRef = inject(DestroyRef);
  private readonly plantGroupApi = inject(PlantGroupApi);
  private readonly retry$ = new Subject<void>();
  private loadMoreSubscription?: Subscription;

  protected readonly skeletonCards = [1, 2, 3, 4, 5, 6];
  protected readonly state = signal<PageState>({ status: 'loading' });

  constructor() {
    // Reloads page 1 whenever the group ID in the URL changes or the user presses Try Again.
    combineLatest([
      this.route.paramMap.pipe(map((params) => params.get('plantGroupId') ?? '')),
      this.retry$.pipe(startWith(undefined)),
    ])
      .pipe(
        // A pending "Load more" belongs to the previous load; drop it.
        tap(() => this.loadMoreSubscription?.unsubscribe()),
        switchMap(([plantGroupId]) => this.loadFirstPage(plantGroupId)),
        takeUntilDestroyed(),
      )
      .subscribe((state) => this.state.set(state));
  }

  protected retry(): void {
    this.retry$.next();
  }

  protected loadMore(): void {
    const current = this.state();
    if (current.status !== 'loaded' || !current.hasNext || current.more === 'loading') {
      return;
    }

    this.state.set({ ...current, more: 'loading' });
    this.loadMoreSubscription = this.plantGroupApi
      .getPlantGroup(current.group.id, current.page + 1)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => this.state.set(appendPage({ ...current, more: 'idle' }, response)),
        // Keep the plants already shown; the button becomes a retry.
        error: () => this.state.set({ ...current, more: 'error' }),
      });
  }

  /** A group opened directly goes back to its own category. */
  protected goBack(): void {
    const current = this.state();
    const fallback =
      current.status === 'loaded' ? `/categories/${current.group.category.id}` : '/explore';
    goBack(this.location, this.router, fallback);
  }

  private loadFirstPage(plantGroupId: string): Observable<PageState> {
    return this.plantGroupApi.getPlantGroup(plantGroupId, 1).pipe(
      map((response): PageState => ({
        status: 'loaded',
        group: response.data,
        plants: response.data.plants,
        page: response.pagination.page,
        hasNext: response.pagination.hasNext,
        more: 'idle',
      })),
      catchError((error: unknown) =>
        of<PageState>(
          error instanceof HttpErrorResponse && error.status === 404
            ? { status: 'not-found' }
            : { status: 'error' },
        ),
      ),
      startWith<PageState>({ status: 'loading' }),
    );
  }
}

/** Adds the next page, skipping plants already shown (the list can shift between requests). */
function appendPage(state: LoadedState, response: PagedResponse<PlantGroupDetail>): LoadedState {
  const shown = new Set(state.plants.map((plant) => plant.id));
  return {
    ...state,
    plants: [...state.plants, ...response.data.plants.filter((plant) => !shown.has(plant.id))],
    page: response.pagination.page,
    hasNext: response.pagination.hasNext,
  };
}
