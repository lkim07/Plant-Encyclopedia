import { Location } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../../app.routes';
import { ApiResponse } from '../../core/api/api-response.model';
import { PlantDetail, QuickCare } from '../../core/api/plant-detail.model';

const PLANT_ID = 'd5e00000-0000-4000-8000-000000000001';
const URL = `/api/plants/${PLANT_ID}`;

function plant(quickCare: QuickCare | null = null): ApiResponse<PlantDetail> {
  return {
    data: {
      id: PLANT_ID,
      name: "Rosa 'Peace'",
      plantGroup: 'Roses',
      scientificName: 'Rosa × hybrida',
      description: 'Development sample data — not verified.',
      heroImage: null,
      quickCare,
    },
  };
}

describe('PlantDetailPage', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function openPlant(): Promise<RouterTestingHarness> {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(`/plants/${PLANT_ID}`);
    return harness;
  }

  async function settle(harness: RouterTestingHarness): Promise<HTMLElement> {
    await harness.fixture.whenStable();
    return harness.routeNativeElement as HTMLElement;
  }

  // Protects against: a blank page while waiting for the API (UX §42).
  it('shows a skeleton while loading', async () => {
    const harness = await openPlant();
    const page = await settle(harness);

    expect(page.querySelector('article')?.getAttribute('aria-busy')).toBe('true');
    expect(page.textContent).toContain('Loading plant…');

    http.expectOne(URL).flush(plant());
  });

  // Protects against: the naming hierarchy or description not rendering (UX §28).
  it('shows the plant names, description and missing-care message', async () => {
    const harness = await openPlant();
    http.expectOne(URL).flush(plant());
    const page = await settle(harness);

    expect(page.querySelector('h1')?.textContent).toBe("Rosa 'Peace'");
    expect(page.querySelector('.group')?.textContent).toBe('Roses');
    expect(page.querySelector('.scientific i')?.textContent).toBe('Rosa × hybrida');
    expect(page.querySelector('.description')?.textContent).toBe('Development sample data — not verified.');
    expect(page.querySelector('.hero-image')).toBeNull();
    expect(page.textContent).toContain("Care information isn't available yet.");
    expect(page.querySelector('article')?.getAttribute('aria-busy')).toBe('false');
  });

  // Protects against: verified care not shown, or cards not expanding (UX §29).
  it('shows Quick Care cards and expands a card with details', async () => {
    const harness = await openPlant();
    http.expectOne(URL).flush(
      plant({
        light: { value: 'Test light', description: 'Test light details' },
        water: null,
        temperature: { minC: 15, maxC: 25, description: null },
        soil: { value: 'Test soil', description: null },
      }),
    );
    const page = await settle(harness);

    const values = Array.from(page.querySelectorAll('app-care-card .value')).map((v) => v.textContent);
    expect(values).toEqual(['Test light', '15–25°C', 'Test soil']);

    // Only the card with details is a button.
    const lightCard = page.querySelector<HTMLButtonElement>('app-care-card button')!;
    expect(page.querySelectorAll('app-care-card button').length).toBe(1);
    expect(lightCard.getAttribute('aria-expanded')).toBe('false');

    lightCard.click();
    await settle(harness);

    expect(lightCard.getAttribute('aria-expanded')).toBe('true');
    expect(lightCard.querySelector('.details')?.textContent).toBe('Test light details');
  });

  // Protects against: unknown or unpublished plants showing an error instead of "not found".
  it('shows "Plant not found" for a 404', async () => {
    const harness = await openPlant();
    http
      .expectOne(URL)
      .flush({ error: { code: 'PLANT_NOT_FOUND', message: 'x' } }, { status: 404, statusText: 'Not Found' });
    const page = await settle(harness);

    expect(page.querySelector('h1')?.textContent).toBe('Plant not found');
    expect(page.querySelector('a[href="/explore"]')).not.toBeNull();
  });

  // Protects against: a malformed ID being shown as a server error.
  it('shows "Plant not found" for a 400', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/plants/abc');
    http
      .expectOne('/api/plants/abc')
      .flush({ error: { code: 'VALIDATION_ERROR', message: 'x' } }, { status: 400, statusText: 'Bad Request' });
    const page = await settle(harness);

    expect(page.querySelector('h1')?.textContent).toBe('Plant not found');
  });

  // Protects against: a failed request leaving the user stuck (UX §44).
  it('shows an error with Try Again, which reloads the plant', async () => {
    const harness = await openPlant();
    http.expectOne(URL).flush(null, { status: 500, statusText: 'Server Error' });
    let page = await settle(harness);

    expect(page.querySelector('[role="alert"] h1')?.textContent).toBe('Something went wrong');

    page.querySelector<HTMLButtonElement>('.action')!.click();
    http.expectOne(URL).flush(plant());
    page = await settle(harness);

    expect(page.querySelector('h1')?.textContent).toBe("Rosa 'Peace'");
  });

  // Protects against: Back leaving the app when the page was opened directly.
  it('Back goes to Identify when there is no in-app history', async () => {
    const harness = await openPlant();
    http.expectOne(URL).flush(plant());
    const page = await settle(harness);
    vi.spyOn(TestBed.inject(Location), 'getState').mockReturnValue({ navigationId: 1 });

    page.querySelector<HTMLButtonElement>('button[aria-label="Back"]')!.click();
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe('/identify');
  });

  // Protects against: Back ignoring the previous in-app screen (PRD §32).
  it('Back returns to the previous screen when there is in-app history', async () => {
    const harness = await openPlant();
    http.expectOne(URL).flush(plant());
    const page = await settle(harness);
    const location = TestBed.inject(Location);
    vi.spyOn(location, 'getState').mockReturnValue({ navigationId: 3 });
    const back = vi.spyOn(location, 'back').mockImplementation(() => {});

    page.querySelector<HTMLButtonElement>('button[aria-label="Back"]')!.click();

    expect(back).toHaveBeenCalledOnce();
  });
});
