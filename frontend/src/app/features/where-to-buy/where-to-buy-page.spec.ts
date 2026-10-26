import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../../app.routes';
import { Retailer, RetailersPage } from '../../core/api/retailer-api';

const PLANT_ID = 'd5e00000-0000-4000-8000-000000000001';
const SHOPS_URL = `/api/plants/${PLANT_ID}/retailers`;

const ATTRIBUTION = {
  name: 'OpenStreetMap',
  notice: '© OpenStreetMap contributors',
  license: 'ODbL 1.0',
  url: 'https://www.openstreetmap.org/copyright',
  retrievedAt: '2026-10-26T04:37:27Z',
};

function shop(n: number, extra: Partial<Retailer> = {}): Retailer {
  return {
    id: `r${n}`,
    name: `Shop ${n}`,
    type: 'gardenCentre',
    location: { address: '12 Main Street', city: 'Vancouver', region: 'BC', distanceKm: 1.1 * n },
    rating: null,
    website: null,
    availability: { status: 'unknown' },
    ...extra,
  };
}

function page(shops: Retailer[], hasNext = false): RetailersPage {
  return { data: shops, pagination: { page: 1, pageSize: 20, hasNext }, attribution: ATTRIBUTION };
}

describe('Where to Buy', () => {
  let http: HttpTestingController;
  let getCurrentPosition: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    getCurrentPosition = vi.fn();
    Object.defineProperty(navigator, 'geolocation', {
      value: { getCurrentPosition },
      configurable: true,
    });
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    Reflect.deleteProperty(navigator, 'geolocation');
  });

  const settle = () => new Promise<void>((resolve) => setTimeout(resolve));

  /** Opens the page; the browser answers with the position, or refuses when none is given. */
  async function open(position?: { latitude: number; longitude: number }) {
    getCurrentPosition.mockImplementation((ok: PositionCallback, fail: PositionErrorCallback) =>
      position
        ? ok({ coords: position } as GeolocationPosition)
        : fail({ code: 1 } as GeolocationPositionError),
    );
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(`/plants/${PLANT_ID}/where-to-buy`);
    http.expectOne(`/api/plants/${PLANT_ID}`).flush({ data: { id: PLANT_ID, name: 'Dog rose' } });
    http.expectOne('/api/retailers/places').flush({
      data: [
        { name: 'Vancouver', region: 'BC', latitude: 49.26, longitude: -123.11 },
        { name: 'Kamloops', region: 'BC', latitude: 50.67, longitude: -120.33 },
      ],
    });
    await settle();
    return harness;
  }

  function shopsRequest(): TestRequest {
    return http.expectOne((req) => req.url === SHOPS_URL);
  }

  const view = (harness: RouterTestingHarness) => harness.routeNativeElement as HTMLElement;

  // Protects against: asking for location anywhere but Where to Buy (D-035), sending a precise position, or claiming
  // stock, or the shop data without its OpenStreetMap credit (D-033, D-073).
  it('asks for the location here, sends it rounded, and lists shops as places to check', async () => {
    const harness = await open({ latitude: 49.281234, longitude: -123.123456 });

    expect(getCurrentPosition).toHaveBeenCalledOnce();
    const request = shopsRequest();
    expect(request.request.params.get('latitude')).toBe('49.281');
    expect(request.request.params.get('longitude')).toBe('-123.123');
    expect(request.request.params.get('sort')).toBe('distance');
    request.flush(
      page([shop(1, { website: 'https://shop.example' }), shop(2, { type: 'flowerShop' })]),
    );
    await harness.fixture.whenStable();

    const page1 = view(harness);
    expect(Array.from(page1.querySelectorAll('.shop h3')).map((h) => h.textContent)).toEqual([
      'Shop 1',
      'Shop 2',
    ]);
    expect(page1.querySelector('.shop .kind')!.textContent!.trim()).toBe('Garden centre · 1.1 km');
    expect(page1.querySelector('.shop .address')!.textContent).toBe('12 Main Street, Vancouver');
    const website = page1.querySelector<HTMLAnchorElement>('.website')!;
    expect(website.getAttribute('href')).toBe('https://shop.example');
    expect(website.getAttribute('rel')).toContain('noopener');
    expect(page1.textContent).toContain("We don't know what each shop has in stock.");
    expect(page1.textContent).not.toMatch(/in stock now|available here/i);
    expect(page1.querySelector('.attribution')!.textContent).toContain(
      '© OpenStreetMap contributors',
    );
  });

  // Protects against: a dead end when location is refused (D-035: Vancouver, BC, and a way to change it).
  it('uses Vancouver, BC when location is refused, says so, and lets the user choose a town', async () => {
    const harness = await open();

    const first = shopsRequest();
    expect([first.request.params.get('latitude'), first.request.params.get('longitude')]).toEqual([
      '49.2827',
      '-123.1207',
    ]);
    first.flush(page([shop(1)]));
    await harness.fixture.whenStable();
    expect(view(harness).textContent).toContain("We couldn't use your location");

    const select = view(harness).querySelector<HTMLSelectElement>('.location select')!;
    expect(Array.from(select.options).map((o) => o.textContent!.trim())).toEqual([
      'Use my location',
      'Vancouver, BC',
      'Kamloops, BC',
    ]);
    select.value = 'place:Kamloops';
    select.dispatchEvent(new Event('change'));
    const kamloops = shopsRequest();
    expect(kamloops.request.params.get('latitude')).toBe('50.67');
    kamloops.flush(page([shop(3)]));
    await harness.fixture.whenStable();
    expect(view(harness).textContent).not.toContain("We couldn't use your location");
  });

  // Protects against: the sort switch not changing the order asked for (UX §34: distance, or relevance).
  it('switches between distance and relevance', async () => {
    const harness = await open({ latitude: 49.28, longitude: -123.12 });
    shopsRequest().flush(page([shop(1)]));
    await harness.fixture.whenStable();

    const relevance = Array.from(
      view(harness).querySelectorAll<HTMLButtonElement>('.sort button'),
    )[1];
    relevance.click();
    const request = shopsRequest();
    expect(request.request.params.get('sort')).toBe('relevance');
    request.flush(page([shop(1)]));
    await harness.fixture.whenStable();
    expect(relevance.getAttribute('aria-pressed')).toBe('true');
  });

  // Protects against: an empty page with nothing to do when no shop is close.
  it('offers a wider search when no shops are nearby', async () => {
    const harness = await open({ latitude: 54.0, longitude: -128.0 });
    shopsRequest().flush(page([]));
    await harness.fixture.whenStable();

    expect(view(harness).textContent).toContain('No shops found within 25 km of Your location.');
    Array.from(view(harness).querySelectorAll<HTMLButtonElement>('button'))
      .find((b) => b.textContent!.includes('Search within 50 km'))!
      .click();
    const wider = shopsRequest();
    expect(wider.request.params.get('radius')).toBe('50');
    wider.flush(page([shop(1)]));
  });
});
