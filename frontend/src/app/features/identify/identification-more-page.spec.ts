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
import { ExploreCard } from '../../core/api/explore-api';

const ROSE = 'd5e00000-0000-4000-8000-000000000001';
const FERN = 'd5e00000-0000-4000-8000-000000000002';

function card(n: number): ExploreCard {
  return {
    id: `d5e00000-0000-4000-8000-0000000001${String(n).padStart(2, '0')}`,
    name: `Plant ${n}`,
    plantGroup: 'Roses',
    imageUrl: null,
  };
}

describe('More (identification)', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    // Infinite scroll needs IntersectionObserver; without it the Load more button is the way to the next page.
    vi.stubGlobal('IntersectionObserver', undefined);
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    vi.unstubAllGlobals();
  });

  async function open(url: string): Promise<RouterTestingHarness> {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    return harness;
  }

  function related(page: number): TestRequest {
    return http.expectOne(
      (req) => req.url === '/api/identification/related' && req.params.get('page') === String(page),
    );
  }

  const names = (harness: RouterTestingHarness) =>
    Array.from(
      (harness.routeNativeElement as HTMLElement).querySelectorAll(
        'ul[aria-label="Related plants"] .name',
      ),
    ).map((n) => n.textContent);

  // Protects against: More not being about this identification (UX §37: "related to this plant", not Explore).
  it('asks for plants related to the candidates in the address and shows them as cards', async () => {
    const harness = await open(`/identify/more?plants=${ROSE},${FERN}`);

    const request = related(1);
    expect(request.request.params.getAll('plantId')).toEqual([ROSE, FERN]);
    request.flush({
      data: [card(1), card(2)],
      pagination: { page: 1, pageSize: 20, hasNext: true },
    });
    await harness.fixture.whenStable();
    expect(names(harness)).toEqual(['Plant 1', 'Plant 2']);

    (harness.routeNativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('.load-more button')!
      .click();
    related(2).flush({
      data: [card(2), card(3)],
      pagination: { page: 2, pageSize: 20, hasNext: false },
    });
    await harness.fixture.whenStable();
    expect(names(harness)).toEqual(['Plant 1', 'Plant 2', 'Plant 3']);
  });

  // Protects against: a tampered address reaching the API, or a blank page without candidates.
  it('uses only up to three valid plant IDs from the address', async () => {
    const harness = await open(
      `/identify/more?plants=${ROSE},not-an-id,${ROSE},${FERN},${card(1).id},${card(2).id}`,
    );
    expect(related(1).request.params.getAll('plantId')).toEqual([ROSE, FERN, card(1).id]);
    expect(harness).toBeDefined();
  });

  it('goes to Identify when the address has no plant IDs', async () => {
    await open('/identify/more?plants=junk');

    expect(TestBed.inject(Router).url).toBe('/identify');
  });

  // Protects against: a dead end when nothing related is in the encyclopedia yet.
  it('says so and offers Explore when there are no related plants', async () => {
    const harness = await open(`/identify/more?plants=${ROSE}`);
    related(1).flush({ data: [], pagination: { page: 1, pageSize: 20, hasNext: false } });
    await harness.fixture.whenStable();

    const page = harness.routeNativeElement as HTMLElement;
    expect(page.textContent).toContain("We don't have related plants for this one yet.");
    expect(page.querySelector('a[href="/explore"]')).not.toBeNull();
  });
});
