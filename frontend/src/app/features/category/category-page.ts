import { Location } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  catchError,
  combineLatest,
  map,
  Observable,
  of,
  startWith,
  Subject,
  switchMap,
} from 'rxjs';
import { CategoryDetail } from '../../core/api/browse.model';
import { CategoryApi } from '../../core/api/category-api';
import { goBack } from '../../core/navigation/go-back';
import { ImageCard } from '../../shared/image-card/image-card';
import { Icon } from '../../shared/icons/icon';
import { TitleRise } from '../../shared/title-rise';

type PageState =
  | { readonly status: 'loading' }
  | { readonly status: 'loaded'; readonly category: CategoryDetail }
  | { readonly status: 'not-found' }
  | { readonly status: 'error' };

/** Category page (UX §22): plant-group cards from this category only (UX §24). */
@Component({
  selector: 'app-category-page',
  imports: [RouterLink, ImageCard, Icon, TitleRise],
  templateUrl: './category-page.html',
  styleUrl: '../../shared/browse-page.css',
})
export class CategoryPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly categoryApi = inject(CategoryApi);
  private readonly retry$ = new Subject<void>();

  protected readonly skeletonCards = [1, 2, 3, 4, 5, 6];

  /** Reloads whenever the category ID in the URL changes or the user presses Try Again. */
  protected readonly state = toSignal(
    combineLatest([
      this.route.paramMap.pipe(map((params) => params.get('categoryId') ?? '')),
      this.retry$.pipe(startWith(undefined)),
    ]).pipe(switchMap(([categoryId]) => this.load(categoryId))),
    { initialValue: { status: 'loading' } as PageState },
  );

  protected retry(): void {
    this.retry$.next();
  }

  protected goBack(): void {
    goBack(this.location, this.router, '/explore');
  }

  private load(categoryId: string): Observable<PageState> {
    return this.categoryApi.getCategory(categoryId).pipe(
      map((category): PageState => ({ status: 'loaded', category })),
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
