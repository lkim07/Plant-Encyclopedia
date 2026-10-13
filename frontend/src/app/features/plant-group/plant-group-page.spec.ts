import { Location } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../../app.routes';
import { PagedResponse } from '../../core/api/api-response.model';
import { PlantCard, PlantGroupDetail } from '../../core/api/browse.model';

const pageUrl = (page: number) => `/api/plant-groups/roses?page=${page}&pageSize=20`;

function plantCard(n: number): PlantCard {
  return {
    id: `d5e00000-0000-4000-8000-0000000000${String(n).padStart(2, '0')}`,
    name: `Test Rose ${n}`,
    imageUrl: null,
  };
}

function groupPage(
  page: number,
  plants: PlantCard[],
  hasNext: boolean,
): PagedResponse<PlantGroupDetail> {
  return {
    data: { id: 'roses', name: 'Roses', category: { id: 'flowers', name: 'Flowers' }, plants },
    pagination: { page, pageSize: 20, hasNext },
  };
}

describe('PlantGroupPage', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function openGroup(): Promise<RouterTestingHarness> {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/plant-groups/roses');
    return harness;
  }

  async function settle(harness: RouterTestingHarness): Promise<HTMLElement> {
    await harness.fixture.whenStable();
    return harness.routeNativeElement as HTMLElement;
  }

  const cardNames = (page: HTMLElement) =>
    Array.from(page.querySelectorAll('app-image-card .name')).map((n) => n.textContent);
  const loadMoreButton = (page: HTMLElement) =>
    page.querySelector<HTMLButtonElement>('.load-more button');

  // Protects against: a blank page while waiting for the API (UX §42).
  it('shows a skeleton while loading', async () => {
    const harness = await openGroup();
    const page = await settle(harness);

    expect(page.querySelector('article')?.getAttribute('aria-busy')).toBe('true');
    expect(page.textContent).toContain('Loading plants…');

    http.expectOne(pageUrl(1)).flush(groupPage(1, [], false));
  });

  // Protects against: plant cards not linking to Plant Detail, or the category context missing (UX §23).
  it('shows the group, its category, and a card linking to each plant', async () => {
    const harness = await openGroup();
    http.expectOne(pageUrl(1)).flush(groupPage(1, [plantCard(1), plantCard(2)], false));
    const page = await settle(harness);

    expect(page.querySelector('h1')?.textContent).toBe('Roses');
    expect(page.querySelector('.eyebrow a')?.getAttribute('href')).toBe('/categories/flowers');
    expect(cardNames(page)).toEqual(['Test Rose 1', 'Test Rose 2']);
    expect(page.querySelector('app-image-card a')?.getAttribute('href')).toBe(
      `/plants/${plantCard(1).id}`,
    );
    expect(loadMoreButton(page)).toBeNull();
  });

  // Protects against: Load more replacing or duplicating plants instead of appending (UX §53).
  it('Load more appends the next page and hides the button on the last page', async () => {
    const harness = await openGroup();
    http.expectOne(pageUrl(1)).flush(groupPage(1, [plantCard(1), plantCard(2)], true));
    let page = await settle(harness);

    loadMoreButton(page)!.click();
    page = await settle(harness);
    expect(loadMoreButton(page)?.disabled).toBe(true);

    // Plant 2 shifted onto page 2 between requests; it must not appear twice.
    http.expectOne(pageUrl(2)).flush(groupPage(2, [plantCard(2), plantCard(3)], false));
    page = await settle(harness);

    expect(cardNames(page)).toEqual(['Test Rose 1', 'Test Rose 2', 'Test Rose 3']);
    expect(loadMoreButton(page)).toBeNull();
  });

  // Protects against: a failed Load more discarding the plants already shown.
  it('keeps loaded plants when Load more fails, and retries', async () => {
    const harness = await openGroup();
    http.expectOne(pageUrl(1)).flush(groupPage(1, [plantCard(1)], true));
    let page = await settle(harness);

    loadMoreButton(page)!.click();
    http.expectOne(pageUrl(2)).flush(null, { status: 500, statusText: 'Server Error' });
    page = await settle(harness);

    expect(cardNames(page)).toEqual(['Test Rose 1']);
    expect(page.querySelector('.load-more [role="alert"]')?.textContent).toBe(
      "We couldn't load more plants.",
    );
    expect(loadMoreButton(page)?.textContent?.trim()).toBe('Try Again');

    loadMoreButton(page)!.click();
    http.expectOne(pageUrl(2)).flush(groupPage(2, [plantCard(2)], false));
    page = await settle(harness);

    expect(cardNames(page)).toEqual(['Test Rose 1', 'Test Rose 2']);
  });

  // Protects against: a group with no published plants looking broken (UX §43).
  it('shows the empty message when the group has no plants', async () => {
    const harness = await openGroup();
    http.expectOne(pageUrl(1)).flush(groupPage(1, [], false));
    const page = await settle(harness);

    expect(page.textContent).toContain('No plants available yet.');
  });

  // Protects against: an unknown group showing an error instead of "not found".
  it('shows "Plant group not found" for a 404', async () => {
    const harness = await openGroup();
    http
      .expectOne(pageUrl(1))
      .flush(
        { error: { code: 'PLANT_GROUP_NOT_FOUND', message: 'x' } },
        { status: 404, statusText: 'Not Found' },
      );
    const page = await settle(harness);

    expect(page.querySelector('h1')?.textContent).toBe('Plant group not found');
    expect(page.querySelector('a[href="/explore"]')).not.toBeNull();
  });

  // Protects against: a failed request leaving the user stuck (UX §44).
  it('shows an error with Try Again, which reloads the group', async () => {
    const harness = await openGroup();
    http.expectOne(pageUrl(1)).flush(null, { status: 500, statusText: 'Server Error' });
    let page = await settle(harness);

    expect(page.querySelector('h1')?.textContent).toBe('Something went wrong');

    page.querySelector<HTMLButtonElement>('.action')!.click();
    http.expectOne(pageUrl(1)).flush(groupPage(1, [plantCard(1)], false));
    page = await settle(harness);

    expect(cardNames(page)).toEqual(['Test Rose 1']);
  });

  // Protects against: Back leaving the app when the group page was opened directly.
  it("Back goes to the group's category when there is no in-app history", async () => {
    const harness = await openGroup();
    http.expectOne(pageUrl(1)).flush(groupPage(1, [], false));
    const page = await settle(harness);
    vi.spyOn(TestBed.inject(Location), 'getState').mockReturnValue({ navigationId: 1 });

    page.querySelector<HTMLButtonElement>('button[aria-label="Back"]')!.click();
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe('/categories/flowers');
    http
      .expectOne('/api/categories/flowers')
      .flush({ data: { id: 'flowers', name: 'Flowers', plantGroups: [] } });
  });
});
