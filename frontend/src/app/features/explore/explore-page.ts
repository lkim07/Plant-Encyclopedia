import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, Observable, of, startWith, Subject, Subscription, switchMap } from 'rxjs';
import { CategorySummary } from '../../core/api/browse.model';
import { CategoryApi } from '../../core/api/category-api';
import { ExploreApi, ExploreCard } from '../../core/api/explore-api';
import { ImageCard } from '../../shared/image-card/image-card';
import { WhenVisible } from '../../shared/when-visible';

type CategoriesState =
  | { readonly status: 'loading' }
  | { readonly status: 'loaded'; readonly categories: readonly CategorySummary[] }
  | { readonly status: 'error' };

type FeedState =
  | { readonly status: 'loading' }
  | { readonly status: 'error' }
  | {
      readonly status: 'loaded';
      readonly cards: readonly ExploreCard[];
      readonly page: number;
      readonly hasNext: boolean;
      /** The next page: not requested, on its way, or failed (Try Again). */
      readonly more: 'idle' | 'loading' | 'error';
    };

/**
 * Explore (UX §12–17): category pills, then the discovery feed of image cards, loaded 20 at a time as the user
 * scrolls (a Load more button is the fallback). The feed order comes from the API (API_SPEC §22).
 * Deferred: personalization, restoring the feed and scroll position on Back, masonry, hover, bookmarks,
 * edge fades and desktop drag scrolling for the pills.
 */
@Component({
  selector: 'app-explore-page',
  imports: [RouterLink, ImageCard, WhenVisible],
  templateUrl: './explore-page.html',
  styleUrl: './explore-page.css',
})
export class ExplorePage {
  private readonly categoryApi = inject(CategoryApi);
  private readonly exploreApi = inject(ExploreApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly retry$ = new Subject<void>();
  private feedSubscription?: Subscription;

  protected readonly skeletonPills = [1, 2, 3, 4, 5, 6];
  protected readonly skeletonCards = [1, 2, 3, 4, 5, 6];

  protected readonly categories = toSignal(
    this.retry$.pipe(
      startWith(undefined),
      switchMap(() => this.loadCategories()),
    ),
    { initialValue: { status: 'loading' } as CategoriesState },
  );

  protected readonly feed = signal<FeedState>({ status: 'loading' });

  constructor() {
    this.loadFeed();
  }

  protected retry(): void {
    this.retry$.next();
  }

  /** The first page, also used by Try Again after an error. */
  protected loadFeed(): void {
    this.feedSubscription?.unsubscribe();
    this.feed.set({ status: 'loading' });
    this.feedSubscription = this.exploreApi
      .getFeed(1)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) =>
          this.feed.set({
            status: 'loaded',
            cards: response.data,
            page: 1,
            hasNext: response.pagination.hasNext,
            more: 'idle',
          }),
        error: () => this.feed.set({ status: 'error' }),
      });
  }

  /** The next page, appended; cards already shown are skipped. */
  protected loadMore(): void {
    const state = this.feed();
    if (state.status !== 'loaded' || !state.hasNext || state.more === 'loading') {
      return;
    }

    this.feed.set({ ...state, more: 'loading' });
    this.feedSubscription = this.exploreApi
      .getFeed(state.page + 1)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          const shown = new Set(state.cards.map((card) => card.id));
          this.feed.set({
            status: 'loaded',
            cards: [...state.cards, ...response.data.filter((card) => !shown.has(card.id))],
            page: state.page + 1,
            hasNext: response.pagination.hasNext,
            more: 'idle',
          });
        },
        error: () => this.feed.set({ ...state, more: 'error' }),
      });
  }

  private loadCategories(): Observable<CategoriesState> {
    return this.categoryApi.getCategories().pipe(
      map((categories): CategoriesState => ({ status: 'loaded', categories })),
      catchError(() => of<CategoriesState>({ status: 'error' })),
      startWith<CategoriesState>({ status: 'loading' }),
    );
  }
}
