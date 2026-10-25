import { CdkDrag, CdkDragDrop, CdkDropList } from '@angular/cdk/drag-drop';
import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ExploreApi } from '../../core/api/explore-api';
import { Auth } from '../../core/auth/auth';
import { Favorite, Favorites } from '../../core/favorites/favorites';
import { Icon } from '../../shared/icons/icon';
import { ImageCard } from '../../shared/image-card/image-card';

/** Decorative plant images in the empty state (UX §41). */
const EMPTY_STATE_IMAGES = 3;

/** How long a touch must stay on a card to enter Arrange mode (UX §40). */
export const LONG_PRESS_MS = 500;
/** A touch that moves further than this is a scroll, not a long press. */
const LONG_PRESS_TOLERANCE_PX = 10;

/**
 * Favorites (PRD §18–21, UX §38–41): the signed-in user's saved plants in their order, as image cards with filled
 * bookmarks. Unbookmarking removes the card at once and offers "Removed from Favorites. Undo". With nothing saved,
 * three decorative plant photos (from the Explore feed) fade toward "Explore Plants →". Signed out, it asks the
 * user to sign in. The list is fetched again each time the page opens.
 *
 * Arrange mode (PRD §20, UX §40) is entered with the Arrange button or, on touch screens, a long press on a card;
 * Done or Esc leaves it. Cards jiggle (a dashed outline with reduced motion), stop acting as links, and show a
 * minus control instead of the bookmark. They can be dragged (Angular CDK, mouse and touch) or moved with the
 * Move earlier / Move later buttons, which also serve keyboard and screen-reader users; each move is announced and
 * saved at once.
 * Deferred: folders.
 */
@Component({
  selector: 'app-favorites-page',
  imports: [RouterLink, ImageCard, Icon, CdkDropList, CdkDrag],
  host: { '(document:keydown.escape)': 'arranging.set(false)' },
  template: `
    @let user = auth.user();
    @let state = favorites.loadState();
    @let saved = favorites.saved();

    <header class="page-header">
      <h1>Favorites</h1>
      @if (user && state === 'loaded' && saved.length > 0) {
        <button type="button" class="arrange-toggle" (click)="arranging.set(!arranging())">
          {{ arranging() ? 'Done' : 'Arrange' }}
        </button>
      }
    </header>

    <span class="visually-hidden" role="status">{{ announcement() }}</span>

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
    } @else if (saved.length === 0) {
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
      <ul
        class="card-grid"
        [class.arranging]="arranging()"
        cdkDropList
        cdkDropListOrientation="mixed"
        [cdkDropListDisabled]="!arranging()"
        (cdkDropListDropped)="drop($event)"
      >
        @for (favorite of saved; track favorite.plant.id; let i = $index) {
          <li
            cdkDrag
            [cdkDragStartDelay]="{ touch: 150, mouse: 0 }"
            (pointerdown)="pressStart($event)"
            (pointermove)="pressMove($event)"
            (pointerup)="pressEnd()"
            (pointercancel)="pressEnd()"
            (contextmenu)="onContextMenu($event)"
          >
            <!-- While arranging, the card is inert: it is not a link, and the controls below take over. -->
            <app-image-card
              [attr.inert]="arranging() ? '' : null"
              [name]="favorite.plant.name"
              [imageUrl]="favorite.plant.imageUrl"
              [link]="'/plants/' + favorite.plant.id"
              [plantId]="arranging() ? null : favorite.plant.id"
              [bookmarkUndo]="true"
            />
            @if (arranging()) {
              <button
                type="button"
                class="icon-button remove"
                [attr.aria-label]="'Remove ' + favorite.plant.name + ' from Favorites'"
                (click)="remove(favorite)"
              >
                <app-icon name="minus" />
              </button>
              <div class="moves">
                <button
                  type="button"
                  class="icon-button"
                  [attr.aria-label]="'Move ' + favorite.plant.name + ' earlier'"
                  [disabled]="i === 0"
                  (click)="moveTo(favorite, i - 1)"
                >
                  <app-icon name="arrow-left" />
                </button>
                <button
                  type="button"
                  class="icon-button"
                  [attr.aria-label]="'Move ' + favorite.plant.name + ' later'"
                  [disabled]="i === saved.length - 1"
                  (click)="moveTo(favorite, i + 1)"
                >
                  <app-icon name="arrow-right" />
                </button>
              </div>
            }
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
  protected readonly arranging = signal(false);
  protected readonly announcement = signal('');

  private readonly empty = computed(
    () => this.favorites.loadState() === 'loaded' && this.favorites.saved().length === 0,
  );
  private decorationsRequested = false;
  private longPress: { timer: ReturnType<typeof setTimeout>; x: number; y: number } | null = null;
  private suppressContextMenu = false;

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

    // Nothing left to arrange.
    effect(() => {
      if (this.favorites.saved().length === 0) {
        this.arranging.set(false);
      }
    });
  }

  protected drop(event: CdkDragDrop<unknown>): void {
    const favorite = this.favorites.saved()[event.previousIndex];
    if (favorite) {
      this.moveTo(favorite, event.currentIndex);
    }
  }

  protected moveTo(favorite: Favorite, index: number): void {
    const count = this.favorites.saved().length;
    if (index < 0 || index >= count) {
      return;
    }
    this.favorites.move(favorite.plant.id, index);
    this.announcement.set(`${favorite.plant.name} moved to position ${index + 1} of ${count}.`);
  }

  protected remove(favorite: Favorite): void {
    this.favorites.toggle({ id: favorite.plant.id, name: favorite.plant.name }, { undo: true });
  }

  /** Touch only: holding a card enters Arrange mode; mouse users have the Arrange button (UX §40). */
  protected pressStart(event: PointerEvent): void {
    this.pressEnd();
    if (event.pointerType !== 'touch' || this.arranging()) {
      return;
    }
    this.longPress = {
      x: event.clientX,
      y: event.clientY,
      timer: setTimeout(() => {
        this.longPress = null;
        this.suppressContextMenu = true;
        this.arranging.set(true);
        this.announcement.set(
          'Arrange mode. Drag cards or use the move buttons. Select Done when finished.',
        );
      }, LONG_PRESS_MS),
    };
  }

  protected pressMove(event: PointerEvent): void {
    if (
      this.longPress &&
      Math.hypot(event.clientX - this.longPress.x, event.clientY - this.longPress.y) >
        LONG_PRESS_TOLERANCE_PX
    ) {
      this.pressEnd();
    }
  }

  protected pressEnd(): void {
    if (this.longPress) {
      clearTimeout(this.longPress.timer);
      this.longPress = null;
    }
  }

  /** The long press is ours: no link menu or text selection callout on top of it. */
  protected onContextMenu(event: Event): void {
    if (this.arranging() || this.suppressContextMenu || this.longPress) {
      event.preventDefault();
      this.suppressContextMenu = false;
    }
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
