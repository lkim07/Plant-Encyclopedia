import {
  afterNextRender,
  Component,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  Injector,
  signal,
  untracked,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { catchError, map, Observable, of, startWith, Subject, Subscription, switchMap } from 'rxjs';
import { CategorySummary } from '../../core/api/browse.model';
import { CategoryApi } from '../../core/api/category-api';
import { ExploreApi, ExploreCard } from '../../core/api/explore-api';
import { Auth } from '../../core/auth/auth';
import { ExploreFeedMemory } from './explore-feed-memory';
import { ImageCard } from '../../shared/image-card/image-card';
import { WhenVisible } from '../../shared/when-visible';
import { Icon } from '../../shared/icons/icon';
import { loadGsap, motionAllowed } from '../../shared/motion';
import { TitleRise } from '../../shared/title-rise';

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
      /** Sent with later pages so they continue the same order. */
      readonly feedAt: string;
      /** The next page: not requested, on its way, or failed (Try Again). */
      readonly more: 'idle' | 'loading' | 'error';
    };

/**
 * Explore (UX §12–17): category pills, then the discovery feed of image cards, loaded 20 at a time as the user
 * scrolls (a Load more button is the fallback). The feed order comes from the API (API_SPEC §22), personalized for
 * signed-in users with enough activity.
 * Back (or Forward) to Explore shows the feed as it was left, at the same scroll position, without loading it again
 * (UX §15); arriving any other way (sidebar, links) loads a fresh feed. A feed is only restored for the same user.
 * Deferred: masonry, hover, edge fades and desktop drag scrolling for the pills.
 */
@Component({
  selector: 'app-explore-page',
  imports: [RouterLink, ImageCard, WhenVisible, Icon, TitleRise],
  templateUrl: './explore-page.html',
  styleUrl: './explore-page.css',
})
export class ExplorePage {
  private readonly categoryApi = inject(CategoryApi);
  private readonly exploreApi = inject(ExploreApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly auth = inject(Auth);
  private readonly memory = inject(ExploreFeedMemory);
  private readonly router = inject(Router);
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
    const restored = this.restoreFeed();
    if (!restored) {
      this.loadFeed();
    }
    this.destroyRef.onDestroy(() => this.saveFeed());

    // The title and category pills slide in once, the first time the categories arrive (when motion is welcome).
    const host: HTMLElement = inject(ElementRef).nativeElement;
    const injector = inject(Injector);
    let introduced = restored; // returning to the page: no intro again
    let intro: { revert(): void } | undefined;
    effect(() => {
      if (this.categories().status !== 'loaded' || introduced || !motionAllowed()) {
        return;
      }
      introduced = true;
      untracked(() =>
        afterNextRender(
          async () => {
            const { gsap } = await loadGsap();
            intro = gsap.context(() => {
              gsap.from('.pills li', {
                opacity: 0,
                y: 14,
                duration: 0.5,
                stagger: 0.04,
                ease: 'power2.out',
              });
            }, host);
          },
          { injector },
        ),
      );
    });
    inject(DestroyRef).onDestroy(() => intro?.revert());
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
            feedAt: response.feedAt,
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
      .getFeed(state.page + 1, state.feedAt)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          const shown = new Set(state.cards.map((card) => card.id));
          this.feed.set({
            ...state,
            cards: [...state.cards, ...response.data.filter((card) => !shown.has(card.id))],
            page: state.page + 1,
            hasNext: response.pagination.hasNext,
            more: 'idle',
          });
        },
        error: () => this.feed.set({ ...state, more: 'error' }),
      });
  }

  /** On Back or Forward, shows the feed this user left and scrolls to where they were. */
  private restoreFeed(): boolean {
    const saved = this.memory.saved;
    const userId = this.auth.user()?.id ?? null;
    if (
      !saved ||
      saved.userId !== userId ||
      this.router.currentNavigation()?.trigger !== 'popstate'
    ) {
      return false;
    }

    this.feed.set({
      status: 'loaded',
      cards: saved.cards,
      page: saved.page,
      hasNext: saved.hasNext,
      feedAt: saved.feedAt,
      more: 'idle',
    });
    afterNextRender(() => window.scrollTo({ top: saved.scrollY, behavior: 'instant' }));
    return true;
  }

  /** Remembers the loaded feed and scroll position when leaving Explore (the page has not scrolled yet). */
  private saveFeed(): void {
    const state = this.feed();
    this.memory.saved =
      state.status === 'loaded'
        ? {
            cards: state.cards,
            page: state.page,
            hasNext: state.hasNext,
            feedAt: state.feedAt,
            scrollY: window.scrollY,
            userId: this.auth.user()?.id ?? null,
          }
        : null;
  }

  private loadCategories(): Observable<CategoriesState> {
    return this.categoryApi.getCategories().pipe(
      map((categories): CategoriesState => ({ status: 'loaded', categories })),
      catchError(() => of<CategoriesState>({ status: 'error' })),
      startWith<CategoriesState>({ status: 'loading' }),
    );
  }
}
