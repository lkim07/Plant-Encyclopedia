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
  Injector,
  untracked,
} from '@angular/core';
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
import { PlantApi } from '../../core/api/plant-api';
import { Auth } from '../../core/auth/auth';
import { ViewHistory } from '../../core/history/view-history';
import { PlantDetail } from '../../core/api/plant-detail.model';
import { goBack } from '../../core/navigation/go-back';
import { CareCard } from './care-card';
import { ImageCreditLine } from './image-credit';
import { formatTemperature } from './format-temperature';
import { BookmarkButton } from '../../shared/bookmark/bookmark-button';
import { Icon, IconName } from '../../shared/icons/icon';
import { isDesktopPointer, loadGsap, motionAllowed } from '../../shared/motion';

type PageState =
  | { readonly status: 'loading' }
  | { readonly status: 'loaded'; readonly plant: PlantDetail }
  | { readonly status: 'not-found' }
  | { readonly status: 'error' };

interface CareCardView {
  readonly key: string;
  readonly icon: IconName;
  readonly label: string;
  readonly value: string;
  readonly description: string | null;
}

/**
 * Plant Detail (UX §25–29): hero, naming hierarchy, description, Quick Care. When motion is welcome, the hero
 * image and its name panel ease in (GSAP), and on desktop the image drifts slower than the page as it scrolls
 * away. The page is complete without any of it.
 * For a signed-in user, each loaded plant is recorded once as a view (a recommendation signal, PRD §17).
 */
@Component({
  selector: 'app-plant-detail-page',
  imports: [RouterLink, CareCard, ImageCreditLine, Icon, BookmarkButton],
  templateUrl: './plant-detail-page.html',
  styleUrl: './plant-detail-page.css',
})
export class PlantDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly plantApi = inject(PlantApi);
  private readonly auth = inject(Auth);
  private readonly viewHistory = inject(ViewHistory);
  private readonly retry$ = new Subject<void>();
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly injector = inject(Injector);
  private heroAnimation?: { revert(): void };
  /** The loaded plant already recorded as a view; a reload (Try Again, another plant) counts again. */
  private recordedView: PlantDetail | null = null;

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
      cards.push({ key: 'light', icon: 'sun', label: 'Light', ...care.light });
    }
    if (care.water) {
      cards.push({ key: 'water', icon: 'droplet', label: 'Water', ...care.water });
    }
    const temperature = care.temperature ? formatTemperature(care.temperature) : null;
    if (care.temperature && temperature) {
      cards.push({
        key: 'temperature',
        icon: 'thermometer',
        label: 'Temp',
        value: temperature,
        description: care.temperature.description,
      });
    }
    if (care.soil) {
      cards.push({ key: 'soil', icon: 'sprout', label: 'Soil', ...care.soil });
    }
    return cards;
  });

  protected retry(): void {
    this.retry$.next();
  }

  /** "taxonomy" → "Taxonomy": the label in front of a source credit. */
  constructor() {
    // Each loaded plant with an image gets its own hero animation; the previous one is undone first.
    effect(() => {
      const state = this.state();
      untracked(() => {
        this.heroAnimation?.revert();
        this.heroAnimation = undefined;
        if (state.status === 'loaded' && state.plant.heroImage && motionAllowed()) {
          const plantId = state.plant.id;
          afterNextRender(() => void this.animateHero(plantId), { injector: this.injector });
        }
      });
    });
    inject(DestroyRef).onDestroy(() => this.heroAnimation?.revert());

    // Waits for the sign-in check when the page is opened directly.
    effect(() => {
      const state = this.state();
      const signedIn = this.auth.signedIn();
      untracked(() => {
        if (state.status === 'loaded' && signedIn && this.recordedView !== state.plant) {
          this.recordedView = state.plant;
          this.viewHistory.record(state.plant.id);
        }
      });
    });
  }

  protected attributionLabel(type: string): string {
    return type.charAt(0).toUpperCase() + type.slice(1);
  }

  /** Back follows in-app history; a plant opened directly goes up to its plant group. */
  protected goBack(): void {
    const state = this.state();
    const groupId = state.status === 'loaded' ? state.plant.plantGroupId : null;
    goBack(this.location, this.router, groupId ? `/plant-groups/${groupId}` : '/explore');
  }

  private async animateHero(plantId: string): Promise<void> {
    const { gsap } = await loadGsap();
    const state = this.state();
    const hero = this.host.nativeElement.querySelector('.hero.has-image');
    if (state.status !== 'loaded' || state.plant.id !== plantId || !hero) {
      return; // the page moved on while GSAP loaded
    }

    const desktop = isDesktopPointer();
    this.heroAnimation = gsap.context(() => {
      gsap.from('.hero-image', {
        opacity: 0,
        scale: 1.03,
        duration: desktop ? 1 : 0.5,
        ease: 'power2.out',
      });
      gsap.from('.names', {
        opacity: 0,
        y: desktop ? 32 : 12,
        duration: desktop ? 0.8 : 0.4,
        delay: 0.15,
        ease: 'power3.out',
      });
      if (desktop) {
        // The image drifts slower than the page as the hero scrolls away.
        const scrollTrigger = { trigger: hero, start: 'top top', end: 'bottom top', scrub: true };
        gsap.to('.hero-image', { yPercent: 10, ease: 'none', scrollTrigger });
        gsap.to('.hero-backdrop', { yPercent: 5, ease: 'none', scrollTrigger });
      }
    }, hero);
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
