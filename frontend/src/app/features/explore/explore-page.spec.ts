import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { Location } from '@angular/common';
import { provideLocationMocks } from '@angular/common/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../../app.routes';
import { ExploreCard } from '../../core/api/explore-api';
import { ExploreFeedMemory } from './explore-feed-memory';

/** Lets tests decide when the end of the feed comes into view. */
class FakeIntersectionObserver {
  static instances: FakeIntersectionObserver[] = [];
  constructor(private readonly callback: IntersectionObserverCallback) {
    FakeIntersectionObserver.instances.push(this);
  }
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {
    FakeIntersectionObserver.instances = FakeIntersectionObserver.instances.filter(
      (i) => i !== this,
    );
  }
  scrollIntoView(): void {
    this.callback(
      [{ isIntersecting: true } as IntersectionObserverEntry],
      this as unknown as IntersectionObserver,
    );
  }
}

const FEED_AT = '2026-10-25T01:00:00.0000000Z';

function card(n: number, imageUrl: string | null = null): ExploreCard {
  return {
    id: `d5e00000-0000-4000-8000-0000000000${String(n).padStart(2, '0')}`,
    name: `Plant ${n}`,
    plantGroup: 'Roses',
    imageUrl,
  };
}

describe('ExplorePage', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    FakeIntersectionObserver.instances = [];
    vi.stubGlobal('IntersectionObserver', FakeIntersectionObserver);
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        provideHttpClient(),
        provideHttpClientTesting(),
        // Back and Forward without a real browser history.
        provideLocationMocks(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  async function openExplore(): Promise<RouterTestingHarness> {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/explore');
    return harness;
  }

  async function settle(harness: RouterTestingHarness): Promise<HTMLElement> {
    await harness.fixture.whenStable();
    return harness.routeNativeElement as HTMLElement;
  }

  /** Later pages continue the first page's feed (API_SPEC §20: feedAt). */
  function feedRequest(page: number): TestRequest {
    return http.expectOne(
      (req) =>
        req.url === '/api/explore' &&
        req.params.get('page') === String(page) &&
        req.params.get('pageSize') === '20' &&
        req.params.get('feedAt') === (page === 1 ? null : FEED_AT),
    );
  }

  function flushFeed(page: number, cards: ExploreCard[], hasNext = false): void {
    feedRequest(page).flush({
      data: cards,
      pagination: { page, pageSize: 20, hasNext },
      feedAt: FEED_AT,
    });
  }

  function cardNames(page: HTMLElement): string[] {
    return Array.from(page.querySelectorAll('ul[aria-label="Plants to discover"] li')).map(
      (li) => li.textContent?.trim() ?? '',
    );
  }

  // Protects against: no way into category browsing (UX §16).
  it('shows a pill linking to each category, in API order', async () => {
    const harness = await openExplore();
    expect((await settle(harness)).textContent).toContain('Loading categories…');

    http.expectOne('/api/categories').flush({
      data: [
        { id: 'flowers', name: 'Flowers', imageUrl: null },
        { id: 'trees', name: 'Trees', imageUrl: null },
      ],
    });
    flushFeed(1, []);
    const page = await settle(harness);

    const pills = Array.from(
      page.querySelectorAll<HTMLAnchorElement>('nav[aria-label="Categories"] a'),
    );
    expect(pills.map((a) => a.textContent)).toEqual(['Flowers', 'Trees']);
    expect(pills.map((a) => a.getAttribute('href'))).toEqual([
      '/categories/flowers',
      '/categories/trees',
    ]);
  });

  // Protects against: no way into Search from Explore (UX §12, D-015).
  it('has a Search icon linking to the Search page', async () => {
    const harness = await openExplore();
    http.expectOne('/api/categories').flush({ data: [] });
    flushFeed(1, []);
    const page = await settle(harness);

    expect(page.querySelector('a[aria-label="Search"]')?.getAttribute('href')).toBe('/search');
    expect(page.querySelector('input')).toBeNull();
  });

  // Protects against: a failed category request leaving Explore stuck (UX §44).
  it('shows an error with Try Again, which reloads the categories', async () => {
    const harness = await openExplore();
    http.expectOne('/api/categories').flush(null, { status: 500, statusText: 'Server Error' });
    flushFeed(1, []);
    let page = await settle(harness);

    expect(page.querySelector('[role="alert"]')?.textContent).toContain(
      "We couldn't load the categories.",
    );

    page.querySelector<HTMLButtonElement>('.retry')!.click();
    http
      .expectOne('/api/categories')
      .flush({ data: [{ id: 'herbs', name: 'Herbs', imageUrl: null }] });
    page = await settle(harness);

    expect(page.querySelector('nav a')?.textContent).toBe('Herbs');
  });

  // Protects against: an empty Explore page, cards without links, or images not shown (UX §17).
  it('shows the feed as image cards linking to Plant Detail', async () => {
    const harness = await openExplore();
    http.expectOne('/api/categories').flush({ data: [] });
    expect((await settle(harness)).textContent).toContain('Loading plants…');

    flushFeed(1, [card(1, '/media/one-medium.webp'), card(2)]);
    const page = await settle(harness);

    expect(cardNames(page)).toEqual(['Plant 1', 'Plant 2']);
    const links = Array.from(page.querySelectorAll<HTMLAnchorElement>('.feed a'));
    expect(links.map((a) => a.getAttribute('href'))).toEqual([
      `/plants/${card(1).id}`,
      `/plants/${card(2).id}`,
    ]);
    expect(page.querySelector('.feed img')?.getAttribute('src')).toBe('/media/one-medium.webp');
    expect(page.querySelector('.load-more')).toBeNull();
  });

  // Protects against: the feed stopping after 20 cards, a card shown twice across pages, or a later page
  // reordered by activity since the first (it sends feedAt).
  it('loads the next page as the end comes into view, without duplicates', async () => {
    const harness = await openExplore();
    http.expectOne('/api/categories').flush({ data: [] });
    flushFeed(1, [card(1), card(2)], true);
    await settle(harness);

    FakeIntersectionObserver.instances.at(-1)!.scrollIntoView();
    flushFeed(2, [card(2), card(3)], false);
    const page = await settle(harness);

    expect(cardNames(page)).toEqual(['Plant 1', 'Plant 2', 'Plant 3']);
    expect(page.querySelector('.load-more')).toBeNull();
  });

  // Protects against: a failed later page losing the loaded cards or leaving no way to retry.
  it('keeps the loaded cards and offers Try Again when a later page fails', async () => {
    const harness = await openExplore();
    http.expectOne('/api/categories').flush({ data: [] });
    flushFeed(1, [card(1)], true);
    let page = await settle(harness);

    page.querySelector<HTMLButtonElement>('.load-more button')!.click();
    feedRequest(2).flush(null, { status: 500, statusText: 'Server Error' });
    page = await settle(harness);

    expect(cardNames(page)).toEqual(['Plant 1']);
    expect(page.querySelector('.load-more [role="alert"]')?.textContent).toContain(
      "We couldn't load more plants.",
    );
    const retry = page.querySelector<HTMLButtonElement>('.load-more button')!;
    expect(retry.textContent?.trim()).toBe('Try Again');

    retry.click();
    flushFeed(2, [card(2)]);
    page = await settle(harness);

    expect(cardNames(page)).toEqual(['Plant 1', 'Plant 2']);
  });

  // Protects against: a blank feed area when there is nothing to show yet.
  it('says so when there are no plants to explore', async () => {
    const harness = await openExplore();
    http.expectOne('/api/categories').flush({ data: [] });
    flushFeed(1, []);
    const page = await settle(harness);

    expect(page.querySelector('.feed')?.textContent).toContain('No plants to explore yet.');
  });

  // Protects against: a failed feed leaving Explore stuck (UX §44).
  it('shows a feed error with Try Again, which reloads the feed', async () => {
    const harness = await openExplore();
    http.expectOne('/api/categories').flush({ data: [] });
    feedRequest(1).flush(null, { status: 500, statusText: 'Server Error' });
    let page = await settle(harness);

    expect(page.querySelector('.feed [role="alert"]')?.textContent).toContain(
      "We couldn't load plants to explore.",
    );

    page.querySelector<HTMLButtonElement>('.feed .retry')!.click();
    flushFeed(1, [card(1)]);
    page = await settle(harness);

    expect(cardNames(page)).toEqual(['Plant 1']);
  });
  describe('returning to Explore', () => {
    /** The router handles Back a moment after the history changes. */
    async function back(): Promise<void> {
      TestBed.inject(Location).back();
      await new Promise<void>((resolve) => setTimeout(resolve));
    }

    const PLANT_URL = `/plants/${card(2).id}`;

    /** Explore with two cards and more to come, scrolled down, then a plant opened from it. */
    async function leaveForAPlant(): Promise<RouterTestingHarness> {
      // In the app this starts with the first navigation; tests navigate without it.
      TestBed.inject(Router).setUpLocationChangeListener();
      const harness = await openExplore();
      http.expectOne('/api/categories').flush({ data: [] });
      flushFeed(1, [card(1), card(2)], true);
      await settle(harness);
      vi.spyOn(window, 'scrollY', 'get').mockReturnValue(640);

      await harness.navigateByUrl(PLANT_URL);
      http
        .expectOne(`/api/plants/${card(2).id}`)
        .flush(
          { error: { code: 'PLANT_NOT_FOUND', message: '' } },
          { status: 404, statusText: '' },
        );
      await settle(harness);
      return harness;
    }

    // Protects against: Back from a plant losing the user's place in a long feed (UX §15).
    it('Back shows the same cards at the same scroll position without loading the feed again', async () => {
      const harness = await leaveForAPlant();
      const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {});

      await back();
      const page = await settle(harness);
      http.expectOne('/api/categories').flush({ data: [] });

      http.expectNone((req) => req.url === '/api/explore');
      expect(cardNames(page)).toEqual(['Plant 1', 'Plant 2']);
      expect(scrollTo).toHaveBeenCalledWith({ top: 640, behavior: 'instant' });

      // The restored feed continues where it was, in the same order.
      FakeIntersectionObserver.instances.at(-1)!.scrollIntoView();
      flushFeed(2, [card(3)]);
      expect(cardNames(await settle(harness))).toEqual(['Plant 1', 'Plant 2', 'Plant 3']);
    });

    // Protects against: a stale feed when the user comes back to Explore on purpose (UX §15: it may refresh).
    it('arriving from a link or the sidebar loads a fresh feed', async () => {
      const harness = await leaveForAPlant();

      await harness.navigateByUrl('/explore');
      http.expectOne('/api/categories').flush({ data: [] });
      flushFeed(1, [card(3)]);

      expect(cardNames(await settle(harness))).toEqual(['Plant 3']);
    });

    // Protects against: one person's personalized feed shown to the next person on a shared device.
    it('does not restore a feed that belonged to someone else', async () => {
      const harness = await leaveForAPlant();
      const memory = TestBed.inject(ExploreFeedMemory);
      memory.saved = { ...memory.saved!, userId: 'someone-else' };

      await back();
      await settle(harness);
      http.expectOne('/api/categories').flush({ data: [] });
      flushFeed(1, [card(3)]);

      expect(cardNames(await settle(harness))).toEqual(['Plant 3']);
    });
  });
});
