import { Location } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../../app.routes';
import { PagedResponse } from '../../core/api/api-response.model';
import { SearchResultItem } from '../../core/api/search.model';

const RECENT_KEY = 'plant-encyclopedia.recent-searches';

/** Lets tests decide when the "bottom of the results" comes into view. */
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

function result(n: number): SearchResultItem {
  return {
    id: `d5e00000-0000-4000-8000-0000000000${String(n).padStart(2, '0')}`,
    name: `Test Rose ${n}`,
    plantGroup: 'Roses',
    scientificName: 'Rosa × hybrida',
    imageUrl: null,
  };
}

function resultsPage(
  n: number,
  results: SearchResultItem[],
  hasNext: boolean,
): PagedResponse<SearchResultItem[]> {
  return { data: results, pagination: { page: n, pageSize: 20, hasNext } };
}

const wait = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

describe('SearchPage', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    FakeIntersectionObserver.instances = [];
    vi.stubGlobal('IntersectionObserver', FakeIntersectionObserver);
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  async function open(url: string): Promise<RouterTestingHarness> {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    return harness;
  }

  async function settle(harness: RouterTestingHarness): Promise<HTMLElement> {
    await harness.fixture.whenStable();
    return harness.routeNativeElement as HTMLElement;
  }

  const searchRequest = (q: string, pageNumber = 1): TestRequest =>
    http.expectOne(
      (r) =>
        r.url === '/api/search' &&
        r.params.get('q') === q &&
        r.params.get('page') === String(pageNumber) &&
        r.params.get('pageSize') === '20',
    );
  const suggestionRequest = (q: string): TestRequest =>
    http.expectOne((r) => r.url === '/api/search/suggestions' && r.params.get('q') === q);
  const noSuggestionRequest = () => http.expectNone((r) => r.url === '/api/search/suggestions');

  const input = (page: HTMLElement) =>
    page.querySelector<HTMLInputElement>('input[type="search"]')!;
  const cardNames = (page: HTMLElement) =>
    Array.from(page.querySelectorAll('app-image-card .name')).map((n) => n.textContent);

  function type(page: HTMLElement, text: string): void {
    input(page).value = text;
    input(page).dispatchEvent(new Event('input'));
  }

  function key(page: HTMLElement, name: string): void {
    input(page).dispatchEvent(new KeyboardEvent('keydown', { key: name, cancelable: true }));
  }

  function pressEnter(page: HTMLElement): void {
    page.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
  }

  // Protects against: Search opening without recent searches or a usable empty state (UX §18, §20).
  it('shows recent searches when nothing is typed, and a hint when there are none', async () => {
    let harness = await open('/search');
    let page = await settle(harness);
    expect(page.textContent).toContain('Search by common, scientific, or cultivar name.');

    localStorage.setItem(RECENT_KEY, JSON.stringify(['peony', 'rose']));
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await open('/search');
    page = await settle(harness);

    expect(Array.from(page.querySelectorAll('.recent-text')).map((b) => b.textContent)).toEqual([
      'peony',
      'rose',
    ]);
    expect(page.querySelector('[aria-label="Remove rose from recent searches"]')).not.toBeNull();
  });

  // Protects against: a search link not running its search, or result cards not opening Plant Detail.
  it('runs the search from ?q= and links each result to Plant Detail', async () => {
    const harness = await open('/search?q=rose');
    let page = await settle(harness);
    expect(page.textContent).toContain('Searching…');
    expect(input(page).value).toBe('rose');

    searchRequest('rose').flush(resultsPage(1, [result(1), result(2)], false));
    page = await settle(harness);

    expect(cardNames(page)).toEqual(['Test Rose 1', 'Test Rose 2']);
    expect(page.querySelector('app-image-card a')?.getAttribute('href')).toBe(
      `/plants/${result(1).id}`,
    );
    expect(page.querySelector('.load-more')).toBeNull();
  });

  // Protects against: Enter not searching, or submitted searches not being remembered.
  it('Enter searches the typed text, updates the URL and saves it as a recent search', async () => {
    const harness = await open('/search');
    let page = await settle(harness);

    type(page, '  peace rose ');
    pressEnter(page);
    page = await settle(harness);

    expect(TestBed.inject(Router).url).toBe('/search?q=peace%20rose');
    searchRequest('peace rose').flush(resultsPage(1, [result(1)], false));
    await settle(harness);
    expect(JSON.parse(localStorage.getItem(RECENT_KEY)!)).toEqual(['peace rose']);

    // The pending suggestion request for the searched text is dropped.
    await wait(300);
    noSuggestionRequest();
  });

  // Protects against: suggestions firing on every keystroke, or keyboard selection not working (UX §19).
  it('suggests after typing pauses, and ↑/↓ + Enter choose a suggestion', async () => {
    const harness = await open('/search');
    let page = await settle(harness);

    type(page, 'p');
    type(page, 'pe');
    type(page, 'pea');
    await wait(300);
    suggestionRequest('pea').flush({
      data: [
        { text: 'Peace', type: 'cultivar' },
        { text: "Rosa 'Peace'", type: 'plant' },
      ],
    });
    page = await settle(harness);

    const options = Array.from(page.querySelectorAll('[role="option"]'));
    expect(options.map((o) => o.querySelector('.text')?.textContent)).toEqual([
      'Peace',
      "Rosa 'Peace'",
    ]);
    expect(options.map((o) => o.querySelector('.type')?.textContent)).toEqual([
      'Cultivar',
      'Plant',
    ]);
    expect(input(page).getAttribute('aria-expanded')).toBe('true');

    key(page, 'ArrowDown');
    key(page, 'ArrowDown');
    page = await settle(harness);
    expect(input(page).getAttribute('aria-activedescendant')).toBe('search-suggestion-1');

    pressEnter(page);
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe("/search?q=Rosa%20'Peace'");
    searchRequest("Rosa 'Peace'").flush(resultsPage(1, [result(1)], false));
  });

  // Protects against: suggestion requests for one character (too broad to be useful).
  it('does not request suggestions for a single character', async () => {
    const harness = await open('/search');
    const page = await settle(harness);

    type(page, 'p');
    await wait(300);

    noSuggestionRequest();
  });

  // Protects against: a suggestion failure (e.g. rate limiting) blocking or alarming the user.
  it('hides suggestions quietly when they fail, and Escape closes the list', async () => {
    const harness = await open('/search');
    let page = await settle(harness);

    type(page, 'pea');
    await wait(300);
    suggestionRequest('pea').flush(null, { status: 429, statusText: 'Too Many Requests' });
    page = await settle(harness);

    expect(page.querySelector('[role="listbox"]')).toBeNull();
    expect(page.querySelector('[role="alert"]')).toBeNull();

    type(page, 'peac');
    await wait(300);
    suggestionRequest('peac').flush({ data: [{ text: 'Peace', type: 'cultivar' }] });
    page = await settle(harness);
    expect(page.querySelector('[role="listbox"]')).not.toBeNull();

    key(page, 'Escape');
    page = await settle(harness);
    expect(page.querySelector('[role="listbox"]')).toBeNull();
  });

  // Protects against: recent searches not re-running, or × not removing them (UX §20).
  it('a recent search runs again when chosen, and × removes it', async () => {
    localStorage.setItem(RECENT_KEY, JSON.stringify(['peony', 'rose']));
    const harness = await open('/search');
    let page = await settle(harness);

    page
      .querySelector<HTMLButtonElement>('[aria-label="Remove peony from recent searches"]')!
      .click();
    page = await settle(harness);
    expect(Array.from(page.querySelectorAll('.recent-text')).map((b) => b.textContent)).toEqual([
      'rose',
    ]);

    page.querySelector<HTMLButtonElement>('.recent-text')!.click();
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe('/search?q=rose');
    searchRequest('rose').flush(resultsPage(1, [], false));
  });

  // Protects against: scrolling not loading more, or pages replacing/duplicating results (UX §21).
  it('loads the next page when the end of the results scrolls into view', async () => {
    const harness = await open('/search?q=rose');
    searchRequest('rose').flush(resultsPage(1, [result(1), result(2)], true));
    let page = await settle(harness);

    FakeIntersectionObserver.instances[0].scrollIntoView();
    page = await settle(harness);
    expect(page.querySelector<HTMLButtonElement>('.load-more button')?.disabled).toBe(true);

    // Result 2 shifted onto page 2 between requests; it must not appear twice.
    searchRequest('rose', 2).flush(resultsPage(2, [result(2), result(3)], false));
    page = await settle(harness);

    expect(cardNames(page)).toEqual(['Test Rose 1', 'Test Rose 2', 'Test Rose 3']);
    expect(page.querySelector('.load-more')).toBeNull();
    expect(FakeIntersectionObserver.instances).toHaveLength(0);
  });

  // Protects against: keyboard users being unable to load more, or a failed page losing results.
  it('Load more works as a fallback and offers Try Again after a failure', async () => {
    const harness = await open('/search?q=rose');
    searchRequest('rose').flush(resultsPage(1, [result(1)], true));
    let page = await settle(harness);

    page.querySelector<HTMLButtonElement>('.load-more button')!.click();
    searchRequest('rose', 2).flush(null, { status: 500, statusText: 'Server Error' });
    page = await settle(harness);

    expect(cardNames(page)).toEqual(['Test Rose 1']);
    expect(page.querySelector('.load-more [role="alert"]')?.textContent).toBe(
      "We couldn't load more plants.",
    );
    // No automatic retry loop while the error is shown.
    expect(page.querySelector('.sentinel')).toBeNull();

    page.querySelector<HTMLButtonElement>('.load-more button')!.click();
    searchRequest('rose', 2).flush(resultsPage(2, [result(2)], false));
    page = await settle(harness);

    expect(cardNames(page)).toEqual(['Test Rose 1', 'Test Rose 2']);
  });

  // Protects against: requesting pages beyond the API's limit (which would return 400).
  it('stops at the last page the API allows', async () => {
    const harness = await open('/search?q=rose');
    searchRequest('rose').flush(resultsPage(50, [result(1)], true));
    const page = await settle(harness);

    expect(page.querySelector('.load-more')).toBeNull();
    expect(page.querySelector('.sentinel')).toBeNull();
    expect(page.querySelector('.end-note')?.textContent?.trim()).toBe(
      'Showing the first 1,000 results. Try a more specific search.',
    );
  });

  // Protects against: an empty result looking broken (UX §43).
  it('shows the no-results message with a link to Identify', async () => {
    const harness = await open('/search?q=xyz');
    searchRequest('xyz').flush(resultsPage(1, [], false));
    const page = await settle(harness);

    expect(page.querySelector('.no-results h2')?.textContent).toBe('No plants found');
    expect(page.textContent).toContain('We couldn\'t find a match for "xyz".');
    expect(page.textContent).toContain('Try another name or search term.');
    expect(page.querySelector('.no-results a')?.getAttribute('href')).toBe('/identify');
    // "xyz" cannot be a scientific name, so Catalogue of Life is not offered.
    expect(page.querySelector('.external-lookup')).toBeNull();
  });

  describe('Catalogue of Life lookup', () => {
    const lookupRequest = (): TestRequest =>
      http.expectOne((r) => r.method === 'POST' && r.url === '/api/search/external');
    const lookupButton = (page: HTMLElement) =>
      page.querySelector<HTMLButtonElement>('.external-lookup .action')!;

    async function openNoResults(q: string): Promise<[RouterTestingHarness, HTMLElement]> {
      const harness = await open(`/search?q=${encodeURIComponent(q)}`);
      searchRequest(q).flush(resultsPage(1, [], false));
      return [harness, await settle(harness)];
    }

    // Protects against: calling the external lookup without the user asking (cost, D-005).
    it('is offered for name-shaped searches and runs only when clicked', async () => {
      const [harness, page] = await openNoResults('Rosa canina');

      expect(page.textContent).toContain('it may be listed in Catalogue of Life');
      expect(lookupButton(page).textContent?.trim()).toBe('Search Catalogue of Life');
      http.expectNone('/api/search/external');

      lookupButton(page).click();
      const request = lookupRequest();
      expect(request.request.body).toEqual({ q: 'Rosa canina' });
      expect((await settle(harness)).textContent).toContain('Searching Catalogue of Life…');

      request.flush({ data: { status: 'notFound' } });
      expect((await settle(harness)).textContent).toContain(
        'Catalogue of Life has no exact match either.',
      );
    });

    // Protects against: an imported plant not being reachable, or shown without its source.
    it('shows a found plant as a card with its source', async () => {
      const [harness, page] = await openNoResults('Rosa canina');

      lookupButton(page).click();
      lookupRequest().flush({
        data: { status: 'found', plant: { ...result(7), name: 'Rosa canina' } },
      });
      const updated = await settle(harness);

      expect(updated.querySelector('.no-results')).toBeNull();
      expect(updated.querySelector('#external-heading')?.textContent).toBe(
        'Found in Catalogue of Life',
      );
      expect(cardNames(updated)).toEqual(['Rosa canina']);
      expect(updated.querySelector('app-image-card a')?.getAttribute('href')).toBe(
        `/plants/${result(7).id}`,
      );
      expect(updated.querySelector('.attribution')?.textContent).toBe(
        'Taxonomy: Catalogue of Life, via GBIF',
      );
    });

    it('explains that an unreviewed plant will appear later', async () => {
      const [harness, page] = await openNoResults('Rosa canina');

      lookupButton(page).click();
      lookupRequest().flush({ data: { status: 'pendingReview' } });

      expect((await settle(harness)).textContent).toContain(
        'We found "Rosa canina" in Catalogue of Life. It will appear here after review.',
      );
    });

    // Protects against: provider outages or rate limits looking like "no such plant" (API_SPEC §53).
    it('distinguishes unavailable and rate-limited lookups, and can retry', async () => {
      const [harness, page] = await openNoResults('Rosa canina');

      lookupButton(page).click();
      lookupRequest().flush(
        { error: { code: 'EXTERNAL_LOOKUP_UNAVAILABLE', message: 'unavailable' } },
        { status: 503, statusText: 'Service Unavailable' },
      );
      let updated = await settle(harness);
      expect(updated.querySelector('.external-lookup [role="alert"]')?.textContent).toBe(
        "Catalogue of Life can't be reached right now. Please try again later.",
      );

      lookupButton(updated).click();
      lookupRequest().flush(null, { status: 429, statusText: 'Too Many Requests' });
      updated = await settle(harness);
      expect(updated.querySelector('.external-lookup [role="alert"]')?.textContent).toBe(
        "You're looking up plants too quickly. Please wait a moment and try again.",
      );

      lookupButton(updated).click();
      lookupRequest().flush({ data: { status: 'pendingReview' } });
      expect((await settle(harness)).textContent).toContain('It will appear here after review.');
    });

    // Protects against: a lookup result from the previous search showing under a new one.
    it('resets when the search changes', async () => {
      const [harness, page] = await openNoResults('Rosa canina');
      lookupButton(page).click();
      lookupRequest().flush({ data: { status: 'notFound' } });
      await settle(harness);

      await harness.navigateByUrl('/search?q=Mentha%20spicata');
      searchRequest('Mentha spicata').flush(resultsPage(1, [], false));
      const updated = await settle(harness);

      expect(lookupButton(updated).textContent?.trim()).toBe('Search Catalogue of Life');
    });
  });

  // Protects against: failures leaving the user stuck, or rate limiting looking like a crash (UX §44).
  it('shows a retryable error, and a specific message when rate-limited', async () => {
    const harness = await open('/search?q=rose');
    searchRequest('rose').flush(null, { status: 500, statusText: 'Server Error' });
    let page = await settle(harness);
    expect(page.querySelector('[role="alert"] h2')?.textContent).toBe('Something went wrong');

    page.querySelector<HTMLButtonElement>('.action')!.click();
    searchRequest('rose').flush(null, { status: 429, statusText: 'Too Many Requests' });
    page = await settle(harness);
    expect(page.querySelector('[role="alert"]')?.textContent).toContain(
      "You're searching too quickly. Please wait a moment and try again.",
    );

    page.querySelector<HTMLButtonElement>('.action')!.click();
    searchRequest('rose').flush(resultsPage(1, [result(1)], false));
    page = await settle(harness);
    expect(cardNames(page)).toEqual(['Test Rose 1']);
  });

  // Protects against: invalid text reaching the API.
  it('explains invalid text without sending a request', async () => {
    const harness = await open('/search?q=!!!');
    let page = await settle(harness);
    expect(page.querySelector('[role="alert"]')?.textContent).toBe(
      'Enter at least one letter or number.',
    );

    type(page, '%%');
    pressEnter(page);
    page = await settle(harness);
    expect(page.querySelector('#search-hint')?.textContent).toBe(
      'Enter at least one letter or number.',
    );
    expect(input(page).getAttribute('aria-invalid')).toBe('true');
    expect(TestBed.inject(Router).url).toBe('/search?q=!!!');
  });

  // Protects against: Back leaving the app when Search was opened directly.
  it('Back goes to Explore when there is no in-app history', async () => {
    const harness = await open('/search');
    const page = await settle(harness);
    vi.spyOn(TestBed.inject(Location), 'getState').mockReturnValue({ navigationId: 1 });

    page.querySelector<HTMLButtonElement>('button[aria-label="Back"]')!.click();
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe('/explore');
    http.expectOne('/api/categories').flush({ data: [] });
    // Explore also loads its feed.
    http
      .expectOne((req) => req.url === '/api/explore')
      .flush({ data: [], pagination: { page: 1, pageSize: 20, hasNext: false } });
  });
});
