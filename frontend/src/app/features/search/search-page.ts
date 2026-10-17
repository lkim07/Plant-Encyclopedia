import { Location } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
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
import { ApiErrorResponse, PagedResponse } from '../../core/api/api-response.model';
import { SEARCH_MAX_PAGE, SEARCH_PAGE_SIZE, SearchApi } from '../../core/api/search-api';
import { ExternalLookupResult, SearchResultItem } from '../../core/api/search.model';
import { goBack } from '../../core/navigation/go-back';
import { RecentSearches } from '../../core/storage/recent-searches';
import { ImageCard } from '../../shared/image-card/image-card';
import { SearchBox } from './search-box';
import { isNameShaped, validateSearchText } from './search-text';

/** The "Search Catalogue of Life" action shown when the local search finds nothing (API_SPEC §27). */
type ExternalLookupState =
  | { readonly status: 'idle' }
  | { readonly status: 'loading' }
  | { readonly status: 'found'; readonly plant: SearchResultItem }
  | { readonly status: 'pending-review' }
  | { readonly status: 'not-found' }
  | { readonly status: 'unavailable' }
  | { readonly status: 'rate-limited' }
  | { readonly status: 'error' };

interface LoadedState {
  readonly status: 'loaded';
  /** Every result loaded so far, across pages. */
  readonly results: readonly SearchResultItem[];
  readonly page: number;
  readonly hasNext: boolean;
  readonly more: 'idle' | 'loading' | 'error';
}

type ResultsState =
  | { readonly status: 'idle' }
  | { readonly status: 'loading' }
  | LoadedState
  | { readonly status: 'rate-limited' }
  | { readonly status: 'invalid'; readonly message: string }
  | { readonly status: 'error' };

/**
 * Search mode (UX §18–21, §43–44). The search lives in the URL (?q=), so reload, Back and sharing work.
 * Results load 20 at a time as the user scrolls, with a Load more button as a fallback.
 */
@Component({
  selector: 'app-search-page',
  imports: [RouterLink, ImageCard, SearchBox],
  templateUrl: './search-page.html',
  styleUrls: ['../../shared/browse-page.css', './search-page.css'],
})
export class SearchPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly destroyRef = inject(DestroyRef);
  private readonly searchApi = inject(SearchApi);
  private readonly recentSearches = inject(RecentSearches);
  private readonly retry$ = new Subject<void>();
  private readonly searchBox = viewChild.required(SearchBox);
  private readonly sentinel = viewChild<ElementRef<HTMLElement>>('sentinel');
  private loadMoreSubscription?: Subscription;
  private lookupSubscription?: Subscription;

  protected readonly maxPage = SEARCH_MAX_PAGE;
  protected readonly resultCap = (SEARCH_MAX_PAGE * SEARCH_PAGE_SIZE).toLocaleString('en-US');
  protected readonly skeletonCards = [1, 2, 3, 4, 5, 6];
  protected readonly recent = this.recentSearches.items;

  /** The submitted search (from the URL). */
  protected readonly query = signal('');
  /** What is in the search box, which may differ from the submitted search while typing. */
  protected readonly text = signal('');
  protected readonly state = signal<ResultsState>({ status: 'idle' });
  protected readonly external = signal<ExternalLookupState>({ status: 'idle' });
  /** The Catalogue of Life action is offered only for text that could be a scientific name. */
  protected readonly canLookUp = computed(() => isNameShaped(this.query()));

  constructor() {
    combineLatest([
      this.route.queryParamMap.pipe(map((params) => (params.get('q') ?? '').trim())),
      this.retry$.pipe(startWith(undefined)),
    ])
      .pipe(
        tap(([q]) => {
          // A pending "load more" or external lookup belongs to the previous search; drop it.
          this.loadMoreSubscription?.unsubscribe();
          this.lookupSubscription?.unsubscribe();
          this.external.set({ status: 'idle' });
          this.query.set(q);
          this.text.set(q);
        }),
        switchMap(([q]) => this.loadFirstPage(q)),
        takeUntilDestroyed(),
      )
      .subscribe((state) => this.state.set(state));

    // Opening Search to type: focus the box. Opening a search link: don't pop up the keyboard.
    afterNextRender(() => {
      if (!this.query()) {
        this.searchBox().focus();
      }
    });

    // Infinite scroll: load the next page when the sentinel below the results comes into view.
    // The sentinel only exists while another page can be loaded, so each new one re-arms the observer.
    effect((onCleanup) => {
      const element = this.sentinel()?.nativeElement;
      if (!element || typeof IntersectionObserver === 'undefined') {
        return;
      }
      const observer = new IntersectionObserver(
        (entries) => {
          if (entries.some((entry) => entry.isIntersecting)) {
            this.loadMore();
          }
        },
        { rootMargin: '400px 0px' },
      );
      observer.observe(element);
      onCleanup(() => observer.disconnect());
    });
  }

  protected submit(text: string): void {
    this.recentSearches.add(text);
    if (text === this.query()) {
      this.retry$.next(); // same URL: navigation would do nothing, so reload explicitly
    } else {
      void this.router.navigate(['/search'], { queryParams: { q: text } });
    }
  }

  protected removeRecent(text: string): void {
    this.recentSearches.remove(text);
  }

  protected retry(): void {
    this.retry$.next();
  }

  /** Asks the backend to look the searched name up in Catalogue of Life. Only on the user's click. */
  protected lookUpExternally(): void {
    if (this.external().status === 'loading') {
      return;
    }

    this.external.set({ status: 'loading' });
    this.lookupSubscription = this.searchApi
      .lookUpExternally(this.query())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => this.external.set(toLookupState(result)),
        error: (error: unknown) => this.external.set(toLookupErrorState(error)),
      });
  }

  protected goBack(): void {
    goBack(this.location, this.router, '/explore');
  }

  protected loadMore(): void {
    const current = this.state();
    if (
      current.status !== 'loaded' ||
      !current.hasNext ||
      current.page >= SEARCH_MAX_PAGE ||
      current.more === 'loading'
    ) {
      return;
    }

    this.state.set({ ...current, more: 'loading' });
    this.loadMoreSubscription = this.searchApi
      .search(this.query(), current.page + 1)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => this.state.set(appendPage({ ...current, more: 'idle' }, response)),
        // Keep the results already shown; the button becomes a retry.
        error: () => this.state.set({ ...current, more: 'error' }),
      });
  }

  private loadFirstPage(q: string): Observable<ResultsState> {
    if (!q) {
      return of({ status: 'idle' });
    }

    const problem = validateSearchText(q);
    if (problem) {
      return of({ status: 'invalid', message: problem });
    }

    return this.searchApi.search(q, 1).pipe(
      map((response): ResultsState => ({
        status: 'loaded',
        results: response.data,
        page: response.pagination.page,
        hasNext: response.pagination.hasNext,
        more: 'idle',
      })),
      catchError((error: unknown) => of(toErrorState(error))),
      startWith<ResultsState>({ status: 'loading' }),
    );
  }
}

function toLookupState(result: ExternalLookupResult): ExternalLookupState {
  switch (result.status) {
    case 'found':
      return { status: 'found', plant: result.plant };
    case 'pendingReview':
      return { status: 'pending-review' };
    default:
      return { status: 'not-found' };
  }
}

function toLookupErrorState(error: unknown): ExternalLookupState {
  if (error instanceof HttpErrorResponse && error.status === 429) {
    return { status: 'rate-limited' };
  }
  if (error instanceof HttpErrorResponse && error.status === 503) {
    return { status: 'unavailable' };
  }
  return { status: 'error' };
}

function toErrorState(error: unknown): ResultsState {
  if (error instanceof HttpErrorResponse && error.status === 429) {
    return { status: 'rate-limited' };
  }
  if (error instanceof HttpErrorResponse && error.status === 400) {
    const body = error.error as Partial<ApiErrorResponse> | null;
    return {
      status: 'invalid',
      message: body?.error?.details?.['q'] ?? 'Please check your search and try again.',
    };
  }
  return { status: 'error' };
}

/** Adds the next page, skipping results already shown (the list can shift between requests). */
function appendPage(
  state: LoadedState,
  response: PagedResponse<readonly SearchResultItem[]>,
): LoadedState {
  const shown = new Set(state.results.map((result) => result.id));
  return {
    ...state,
    results: [...state.results, ...response.data.filter((result) => !shown.has(result.id))],
    page: response.pagination.page,
    hasNext: response.pagination.hasNext,
  };
}
