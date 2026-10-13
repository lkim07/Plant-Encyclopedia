import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, Observable, of, startWith, Subject, switchMap } from 'rxjs';
import { CategorySummary } from '../../core/api/browse.model';
import { CategoryApi } from '../../core/api/category-api';

type CategoriesState =
  | { readonly status: 'loading' }
  | { readonly status: 'loaded'; readonly categories: readonly CategorySummary[] }
  | { readonly status: 'error' };

/**
 * Explore (UX §12, §16): category pills leading into category browsing.
 * Deferred: the discovery feed, the Search action, edge fades and desktop drag scrolling.
 */
@Component({
  selector: 'app-explore-page',
  imports: [RouterLink],
  templateUrl: './explore-page.html',
  styleUrl: './explore-page.css',
})
export class ExplorePage {
  private readonly categoryApi = inject(CategoryApi);
  private readonly retry$ = new Subject<void>();

  protected readonly skeletonPills = [1, 2, 3, 4, 5, 6];

  protected readonly categories = toSignal(
    this.retry$.pipe(
      startWith(undefined),
      switchMap(() => this.load()),
    ),
    { initialValue: { status: 'loading' } as CategoriesState },
  );

  protected retry(): void {
    this.retry$.next();
  }

  private load(): Observable<CategoriesState> {
    return this.categoryApi.getCategories().pipe(
      map((categories): CategoriesState => ({ status: 'loaded', categories })),
      catchError(() => of<CategoriesState>({ status: 'error' })),
      startWith<CategoriesState>({ status: 'loading' }),
    );
  }
}
