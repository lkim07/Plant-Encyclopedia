import { Location } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../../app.routes';
import { ApiResponse } from '../../core/api/api-response.model';
import { CategoryDetail, PlantGroupCard } from '../../core/api/browse.model';

const URL = '/api/categories/flowers';

function category(plantGroups: PlantGroupCard[]): ApiResponse<CategoryDetail> {
  return { data: { id: 'flowers', name: 'Flowers', plantGroups } };
}

describe('CategoryPage', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function openCategory(): Promise<RouterTestingHarness> {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/categories/flowers');
    return harness;
  }

  async function settle(harness: RouterTestingHarness): Promise<HTMLElement> {
    await harness.fixture.whenStable();
    return harness.routeNativeElement as HTMLElement;
  }

  // Protects against: a blank page while waiting for the API (UX §42).
  it('shows a skeleton while loading', async () => {
    const harness = await openCategory();
    const page = await settle(harness);

    expect(page.querySelector('article')?.getAttribute('aria-busy')).toBe('true');
    expect(page.textContent).toContain('Loading category…');

    http.expectOne(URL).flush(category([]));
  });

  // Protects against: group cards missing, reordered, or not linking to their group page (UX §22).
  it('shows the category title and a card linking to each plant group', async () => {
    const harness = await openCategory();
    http.expectOne(URL).flush(
      category([
        { id: 'roses', name: 'Roses', imageUrl: null },
        { id: 'tulips', name: 'Tulips', imageUrl: null },
      ]),
    );
    const page = await settle(harness);

    expect(page.querySelector('h1')?.textContent).toBe('Flowers');
    const cards = Array.from(page.querySelectorAll<HTMLAnchorElement>('app-image-card a'));
    expect(cards.map((a) => a.querySelector('.name')?.textContent)).toEqual(['Roses', 'Tulips']);
    expect(cards.map((a) => a.getAttribute('href'))).toEqual([
      '/plant-groups/roses',
      '/plant-groups/tulips',
    ]);
    expect(page.querySelector('app-image-card img')).toBeNull();
  });

  // Protects against: an empty category being filled with unrelated content (UX §43).
  it('shows the empty message when the category has no plant groups', async () => {
    const harness = await openCategory();
    http.expectOne(URL).flush(category([]));
    const page = await settle(harness);

    expect(page.textContent).toContain('No plants available yet.');
    expect(page.querySelector('app-image-card')).toBeNull();
  });

  // Protects against: an unknown category showing an error instead of "not found".
  it('shows "Category not found" for a 404', async () => {
    const harness = await openCategory();
    http
      .expectOne(URL)
      .flush(
        { error: { code: 'CATEGORY_NOT_FOUND', message: 'x' } },
        { status: 404, statusText: 'Not Found' },
      );
    const page = await settle(harness);

    expect(page.querySelector('h1')?.textContent).toBe('Category not found');
    expect(page.querySelector('a[href="/explore"]')).not.toBeNull();
  });

  // Protects against: a failed request leaving the user stuck (UX §44).
  it('shows an error with Try Again, which reloads the category', async () => {
    const harness = await openCategory();
    http.expectOne(URL).flush(null, { status: 500, statusText: 'Server Error' });
    let page = await settle(harness);

    expect(page.querySelector('h1')?.textContent).toBe('Something went wrong');

    page.querySelector<HTMLButtonElement>('.action')!.click();
    http.expectOne(URL).flush(category([]));
    page = await settle(harness);

    expect(page.querySelector('h1')?.textContent).toBe('Flowers');
  });

  // Protects against: Back leaving the app when the page was opened directly.
  it('Back goes to Explore when there is no in-app history', async () => {
    const harness = await openCategory();
    http.expectOne(URL).flush(category([]));
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
