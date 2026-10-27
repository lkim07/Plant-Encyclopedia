import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { ExploreCard } from '../../core/api/explore-api';
import { IdentificationApi } from '../../core/api/identification-api';
import { ImageCard } from '../../shared/image-card/image-card';
import { WhenVisible } from '../../shared/when-visible';
import { TitleRise } from '../../shared/title-rise';

/** Candidates a More page is based on (D-012). */
const MAX_CANDIDATES = 3;

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

type State =
  | { readonly status: 'loading' }
  | { readonly status: 'error' }
  | {
      readonly status: 'loaded';
      readonly cards: readonly ExploreCard[];
      readonly page: number;
      readonly hasNext: boolean;
      readonly more: 'idle' | 'loading' | 'error';
    };

/**
 * More (PRD §30, UX §37): plants related to the identification's candidates, closest first (same plant group, genus,
 * family, then category), as an image grid with infinite scroll. It is not a fourth candidate and not Explore: it is
 * centered on this identification. The candidates are in the address (`?plants=id,id,id`), so a reload or Back keeps
 * the page; without them it goes to Identify.
 */
@Component({
  selector: 'app-identification-more-page',
  imports: [RouterLink, ImageCard, WhenVisible, TitleRise],
  template: `
    <header class="more-header">
      <h1 appTitleRise>More like your plant</h1>
      <p class="lead">Plants related to your possible matches, closest first.</p>
    </header>

    @let s = state();
    @if (s.status === 'loading') {
      <span class="visually-hidden" role="status">Loading related plants…</span>
      <ul class="card-grid" aria-hidden="true">
        @for (card of skeletonCards; track card) {
          <li class="card-skeleton"></li>
        }
      </ul>
    } @else if (s.status === 'error') {
      <div class="notice" role="alert">
        <p>We couldn't load related plants.</p>
        <button type="button" class="retry" (click)="load()">Try Again</button>
      </div>
    } @else if (s.cards.length === 0) {
      <div class="notice">
        <p>We don't have related plants for this one yet.</p>
        <a class="retry" routerLink="/explore">Explore Plants →</a>
      </div>
    } @else {
      <ul class="card-grid" aria-label="Related plants">
        @for (card of s.cards; track card.id) {
          <li>
            <app-image-card
              [name]="card.name"
              [imageUrl]="card.imageUrl"
              [link]="'/plants/' + card.id"
              [plantId]="card.id"
            />
          </li>
        }
      </ul>
      @if (s.hasNext) {
        @if (s.more === 'idle') {
          <div class="sentinel" aria-hidden="true" appWhenVisible (visible)="loadMore()"></div>
        }
        <div class="load-more">
          @if (s.more === 'error') {
            <p role="alert">We couldn't load more plants.</p>
          }
          <button
            type="button"
            class="retry"
            [disabled]="s.more === 'loading'"
            (click)="loadMore()"
          >
            {{ s.more === 'loading' ? 'Loading…' : s.more === 'error' ? 'Try Again' : 'Load more' }}
          </button>
        </div>
      }
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .more-header {
      margin-bottom: var(--space-6);
    }
    h1 {
      margin: 0;
    }
    .lead,
    .notice p {
      margin: var(--space-1) 0 0;
      color: var(--color-text-muted);
    }
    .card-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: var(--space-4);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    @media (min-width: 48rem) {
      .card-grid {
        grid-template-columns: repeat(auto-fill, minmax(11rem, 1fr));
      }
    }
    .card-skeleton {
      aspect-ratio: 4 / 5;
      border-radius: var(--radius-md);
      background: var(--color-surface);
    }
    .notice,
    .load-more {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--space-3);
      margin-top: var(--space-6);
      text-align: center;
    }
    .sentinel {
      height: 1px;
    }
    .retry {
      display: inline-flex;
      align-items: center;
      min-height: var(--touch-target);
      padding: 0 var(--space-4);
      border: 1px solid var(--color-text);
      border-radius: var(--radius-sm);
      background: transparent;
      color: inherit;
      font: inherit;
      font-weight: 600;
      text-decoration: none;
      cursor: pointer;
    }
  `,
})
export class IdentificationMorePage {
  private readonly api = inject(IdentificationApi);
  private readonly destroyRef = inject(DestroyRef);
  private request?: Subscription;
  private readonly plantIds: readonly string[];

  protected readonly skeletonCards = [1, 2, 3, 4, 5, 6];
  protected readonly state = signal<State>({ status: 'loading' });

  constructor() {
    const raw = inject(ActivatedRoute).snapshot.queryParamMap.get('plants') ?? '';
    // The address is user input: only plant IDs, at most three, each once.
    this.plantIds = [...new Set(raw.split(',').filter((id) => GUID.test(id)))].slice(
      0,
      MAX_CANDIDATES,
    );
    if (this.plantIds.length === 0) {
      void inject(Router).navigateByUrl('/identify', { replaceUrl: true });
      return;
    }
    this.load();
  }

  /** The first page, also used by Try Again. */
  protected load(): void {
    this.request?.unsubscribe();
    this.state.set({ status: 'loading' });
    this.request = this.api
      .getRelated(this.plantIds, 1)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) =>
          this.state.set({
            status: 'loaded',
            cards: response.data,
            page: 1,
            hasNext: response.pagination.hasNext,
            more: 'idle',
          }),
        error: () => this.state.set({ status: 'error' }),
      });
  }

  protected loadMore(): void {
    const state = this.state();
    if (state.status !== 'loaded' || !state.hasNext || state.more === 'loading') {
      return;
    }

    this.state.set({ ...state, more: 'loading' });
    this.request = this.api
      .getRelated(this.plantIds, state.page + 1)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          const shown = new Set(state.cards.map((card) => card.id));
          this.state.set({
            ...state,
            cards: [...state.cards, ...response.data.filter((card) => !shown.has(card.id))],
            page: state.page + 1,
            hasNext: response.pagination.hasNext,
            more: 'idle',
          });
        },
        error: () => this.state.set({ ...state, more: 'error' }),
      });
  }
}
