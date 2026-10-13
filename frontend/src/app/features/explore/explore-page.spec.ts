import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../../app.routes';

describe('ExplorePage', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function openExplore(): Promise<RouterTestingHarness> {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/explore');
    return harness;
  }

  async function settle(harness: RouterTestingHarness): Promise<HTMLElement> {
    await harness.fixture.whenStable();
    return harness.routeNativeElement as HTMLElement;
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

  // Protects against: a failed category request leaving Explore stuck (UX §44).
  it('shows an error with Try Again, which reloads the categories', async () => {
    const harness = await openExplore();
    http.expectOne('/api/categories').flush(null, { status: 500, statusText: 'Server Error' });
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
});
