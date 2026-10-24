import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ExploreApi } from '../../core/api/explore-api';
import { Auth } from '../../core/auth/auth';
import { Favorites } from '../../core/favorites/favorites';
import { ImageCard } from '../../shared/image-card/image-card';

/** Decorative plant images in the empty state (UX §41). */
const EMPTY_STATE_IMAGES = 3;

/**
 * Favorites (PRD §18–21, UX §38–41): the signed-in user's saved plants in their order, as image cards with filled
 * bookmarks. Unbookmarking removes the card at once and offers "Removed from Favorites. Undo". With nothing saved,
 * three decorative plant photos (from the Explore feed) fade toward "Explore Plants →". Signed out, it asks the
 * user to sign in. The list is fetched again each time the page opens.
 * Deferred: Arrange mode (6d), folders.
 */
@Component({
  selector: 'app-favorites-page',
  imports: [RouterLink, ImageCard],
  template: `
    <h1>Favorites</h1>

    @let user = auth.user();
    @let state = favorites.loadState();

    @if (user === null) {
      <section class="notice">
        <p>Sign in to see the plants you've saved.</p>
        <a class="action" routerLink="/login" [queryParams]="{ returnUrl: '/favorites' }"
          >Sign in</a
        >
      </section>
    } @else if (user === undefined || state === 'idle' || state === 'loading') {
      <span class="visually-hidden" role="status">Loading your Favorites…</span>
      <ul class="card-grid" aria-hidden="true">
        @for (card of skeletonCards; track card) {
          <li class="card-skeleton"></li>
        }
      </ul>
    } @else if (state === 'error') {
      <div class="notice" role="alert">
        <p>We couldn't load your Favorites.</p>
        <button type="button" class="retry" (click)="favorites.reload()">Try Again</button>
      </div>
    } @else if (favorites.saved().length === 0) {
      <section class="empty">
        <div class="decorations" aria-hidden="true">
          @for (url of decorations(); track url) {
            <img [src]="url" alt="" />
          }
        </div>
        <p>Save plants you love with the bookmark, and find them here.</p>
        <a class="action" routerLink="/explore">Explore Plants →</a>
      </section>
    } @else {
      <ul class="card-grid">
        @for (favorite of favorites.saved(); track favorite.plant.id) {
          <li>
            <app-image-card
              [name]="favorite.plant.name"
              [imageUrl]="favorite.plant.imageUrl"
              [link]="'/plants/' + favorite.plant.id"
              [plantId]="favorite.plant.id"
              [bookmarkUndo]="true"
            />
          </li>
        }
      </ul>
    }
  `,
  styleUrl: './favorites-page.css',
})
export class FavoritesPage {
  protected readonly auth = inject(Auth);
  protected readonly favorites = inject(Favorites);
  private readonly exploreApi = inject(ExploreApi);

  protected readonly skeletonCards = [1, 2, 3, 4];
  protected readonly decorations = signal<readonly string[]>([]);

  private readonly empty = computed(
    () => this.favorites.loadState() === 'loaded' && this.favorites.saved().length === 0,
  );
  private decorationsRequested = false;

  constructor() {
    // Another tab may have changed the list since it was loaded at sign-in.
    if (this.favorites.loadState() === 'loaded') {
      void this.favorites.reload();
    }

    effect(() => {
      if (this.empty() && !this.decorationsRequested) {
        this.decorationsRequested = true;
        untracked(() => this.loadDecorations());
      }
    });
  }

  /** Decoration only: without images (or on an error) the empty state still has its text and link. */
  private loadDecorations(): void {
    this.exploreApi.getFeed(1).subscribe({
      next: (page) =>
        this.decorations.set(
          page.data
            .flatMap((card) => (card.imageUrl ? [card.imageUrl] : []))
            .slice(0, EMPTY_STATE_IMAGES),
        ),
      error: () => this.decorations.set([]),
    });
  }
}
