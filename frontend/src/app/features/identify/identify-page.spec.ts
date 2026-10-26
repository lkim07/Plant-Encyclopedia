import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../../app.routes';
import { IdentificationCandidate } from '../../core/api/identification-api';

const URL_IDENTIFY = '/api/identification';

function candidate(
  rank: number,
  name: string,
  extra: Partial<IdentificationCandidate['plant']> = {},
): IdentificationCandidate {
  return {
    rank,
    plant: {
      id: `d5e00000-0000-4000-8000-00000000000${rank}`,
      name,
      plantGroup: 'Roses',
      plantGroupId: 'roses',
      scientificName: `Rosa example${rank}`,
      imageUrl: `/media/${rank}-medium.webp`,
      ...extra,
    },
  };
}

describe('Identify', () => {
  let http: HttpTestingController;
  const { createObjectURL, revokeObjectURL } = URL;

  beforeEach(() => {
    // jsdom has no object URLs; these stand in and are put back after each test.
    URL.createObjectURL = vi.fn(() => 'blob:photo');
    URL.revokeObjectURL = vi.fn();
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    URL.createObjectURL = createObjectURL;
    URL.revokeObjectURL = revokeObjectURL;
    vi.restoreAllMocks();
  });

  async function open(
    url = '/identify',
  ): Promise<{ harness: RouterTestingHarness; page: () => HTMLElement }> {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    await harness.fixture.whenStable();
    return { harness, page: () => harness.routeNativeElement as HTMLElement };
  }

  function photo(type = 'image/jpeg', size?: number): File {
    const file = new File([new Uint8Array([0xff, 0xd8, 0xff])], 'plant.jpg', { type });
    if (size !== undefined) {
      Object.defineProperty(file, 'size', { value: size });
    }
    return file;
  }

  /** Simulates choosing a file in one of the hidden choosers. */
  async function choose(
    harness: RouterTestingHarness,
    file: File,
    chooser: 'camera' | 'library' = 'library',
  ): Promise<void> {
    const inputs = (harness.routeNativeElement as HTMLElement).querySelectorAll<HTMLInputElement>(
      'input[type="file"]',
    );
    const input = chooser === 'camera' ? inputs[0] : inputs[1];
    Object.defineProperty(input, 'files', { value: [file], configurable: true });
    input.dispatchEvent(new Event('change'));
    await harness.fixture.whenStable();
  }

  const button = (page: HTMLElement, text: string) =>
    Array.from(page.querySelectorAll<HTMLButtonElement>('button')).find(
      (b) => b.textContent!.trim() === text,
    ) ?? null;

  // Protects against: no way to take or choose a photo (UX §6–7), or the camera not opening on phones.
  it('offers Take a Photo (the back camera) and Choose from Photos', async () => {
    const { page } = await open();
    const [camera, library] = Array.from(
      page().querySelectorAll<HTMLInputElement>('input[type="file"]'),
    );
    const opened = vi.spyOn(camera, 'click').mockImplementation(() => {});

    button(page(), 'Take a Photo')!.click();

    expect(opened).toHaveBeenCalledOnce();
    expect(camera.getAttribute('capture')).toBe('environment');
    expect(library.hasAttribute('capture')).toBe(false);
    expect(button(page(), 'Choose from Photos')).not.toBeNull();
    expect(page().querySelector('input[type="search"], input[type="text"]')).toBeNull(); // no search field (UX §6)
  });

  // Protects against: identification starting without the user confirming the photo (UX §8).
  it('previews the chosen photo with Retake and Identify, and sends nothing yet', async () => {
    const { harness, page } = await open();

    await choose(harness, photo());

    expect(page().querySelector<HTMLImageElement>('.photo img')!.getAttribute('src')).toBe(
      'blob:photo',
    );
    expect(button(page(), 'Retake')).not.toBeNull();
    expect(button(page(), 'Identify')).not.toBeNull();
    http.expectNone(URL_IDENTIFY);
  });

  // Protects against: pointless uploads of files the API refuses.
  it('refuses other file types and photos over 10 MB before uploading', async () => {
    const { harness, page } = await open();

    await choose(harness, photo('image/gif'));
    expect(page().querySelector('[role="alert"]')!.textContent).toContain('JPEG, PNG or WebP');
    await choose(harness, photo('image/jpeg', 11 * 1024 * 1024));
    expect(page().querySelector('[role="alert"]')!.textContent).toContain('10 MB');

    expect(page().querySelector('.photo')).toBeNull();
  });

  // Protects against: a spinner replacing the photo (D-046), or results that skip the results page (D-014),
  // show confidence values (D-013) or treat More as a candidate (D-012).
  it('identifies with the photo in view, then shows #1, #2, #3 and More', async () => {
    const { harness, page } = await open();
    await choose(harness, photo(), 'camera');

    button(page(), 'Identify')!.click();
    await harness.fixture.whenStable();
    const request = http.expectOne({ method: 'POST', url: URL_IDENTIFY });
    expect((request.request.body as FormData).get('image')).toBeInstanceOf(File);
    expect(page().querySelector('.photo img')).not.toBeNull();
    expect(page().querySelector('[role="status"]')!.textContent).toBe('Identifying your plant…');
    expect(button(page(), 'Identify')!.disabled).toBe(true);

    request.flush({
      data: {
        status: 'completed',
        candidates: [
          candidate(1, 'Dog rose'),
          candidate(2, 'Sweet briar'),
          candidate(3, 'Field rose', { imageUrl: null }),
        ],
      },
    });
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/identify/results');
    const results = page();
    expect(results.querySelector('.first .name')!.textContent).toBe('Dog rose');
    expect(results.querySelector('.first .group')!.textContent).toBe('Roses');
    expect(results.querySelector('.first .scientific')!.textContent).toBe('Rosa example1');
    expect(results.querySelector('.first a')!.getAttribute('href')).toBe(
      `/plants/${candidate(1, '').plant.id}`,
    );
    expect(
      Array.from(results.querySelectorAll('.rest > li .name')).map((n) => n.textContent),
    ).toEqual(['Sweet briar', 'Field rose']);
    expect(results.querySelector('.rest')!.getAttribute('start')).toBe('2');
    const more = results.querySelector<HTMLAnchorElement>('a.more')!;
    expect(more.getAttribute('href')).toBe('/plant-groups/roses');
    expect(more.closest('ol')).toBeNull(); // More is not a fourth candidate
    expect(results.textContent).not.toContain('%');
  });

  // Protects against: a dead end or a false "this plant does not exist" when nothing matched (UX §44).
  it('keeps the photo and offers Try Again and Choose Another Photo when the plant is not identified', async () => {
    const { harness, page } = await open();
    await choose(harness, photo());
    button(page(), 'Identify')!.click();
    await harness.fixture.whenStable();

    http
      .expectOne(URL_IDENTIFY)
      .flush(
        { error: { code: 'IDENTIFICATION_FAILED', message: '' } },
        { status: 422, statusText: '' },
      );
    await harness.fixture.whenStable();

    const alert = page().querySelector('[role="alert"]')!;
    expect(alert.querySelector('h2')!.textContent).toBe("We couldn't identify this plant");
    expect(alert.textContent).toContain('may not be in our database yet');
    expect(page().querySelector('.photo img')).not.toBeNull();
    expect(button(page(), 'Choose Another Photo')).not.toBeNull();

    button(page(), 'Try Again')!.click();
    await harness.fixture.whenStable();
    http
      .expectOne(URL_IDENTIFY)
      .flush(
        { error: { code: 'IDENTIFICATION_UNAVAILABLE', message: '' } },
        { status: 503, statusText: '' },
      );
    await harness.fixture.whenStable();
    expect(page().querySelector('[role="alert"] h2')!.textContent).toBe(
      "Identification isn't available right now",
    );
  });

  // Protects against: a blank results page after a reload, when the photo and results are gone.
  it('sends the results page back to Identify when there is nothing to show', async () => {
    await open('/identify/results');

    expect(TestBed.inject(Router).url).toBe('/identify');
  });

  // Protects against: Back from the results losing the photo (D-014: Identify → Results → Candidate).
  it('shows the same photo again when going back from the results', async () => {
    const { harness, page } = await open();
    await choose(harness, photo());
    button(page(), 'Identify')!.click();
    await harness.fixture.whenStable();
    http
      .expectOne(URL_IDENTIFY)
      .flush({ data: { status: 'completed', candidates: [candidate(1, 'Dog rose')] } });
    await harness.fixture.whenStable();

    await harness.navigateByUrl('/identify');

    expect(page().querySelector<HTMLImageElement>('.photo img')!.getAttribute('src')).toBe(
      'blob:photo',
    );
    expect(button(page(), 'Identify')).not.toBeNull();
  });
});
