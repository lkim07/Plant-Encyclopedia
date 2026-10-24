import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../../app.routes';
import { SavePrompt } from '../../shared/bookmark/save-prompt';
import { ImageCard } from '../../shared/image-card/image-card';
import { Snackbar, SnackbarHost } from '../../shared/snackbar/snackbar';
import { Auth, User } from '../auth/auth';
import { Favorite, Favorites } from './favorites';

const USER: User = {
  id: 'u1',
  displayName: 'Rose Lover',
  email: 'rose@example.org',
  emailConfirmed: true,
};
const ROSE_ID = '0198a7a0-0000-7000-8000-000000000001';
const FERN_ID = '0198a7a0-0000-7000-8000-000000000002';
const MINT_ID = '0198a7a0-0000-7000-8000-000000000003';

function favorite(plantId: string, name: string, order: number): Favorite {
  return {
    favoriteId: `f-${name}`,
    order,
    plant: { id: plantId, name, plantGroup: null, imageUrl: `/media/${name}.webp` },
  };
}

@Component({
  imports: [ImageCard, SavePrompt, SnackbarHost],
  template: `
    <app-image-card name="Dog rose" link="/plants/rose" [plantId]="plantId" />
    <app-save-prompt />
    <app-snackbar />
  `,
})
class CardHost {
  readonly plantId = ROSE_ID;
}

describe('Favorites', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    // jsdom has no <dialog> methods; these stand in for the browser's.
    HTMLDialogElement.prototype.showModal = function (this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    HTMLDialogElement.prototype.close = function (this: HTMLDialogElement) {
      this.removeAttribute('open');
      this.dispatchEvent(new Event('close'));
    };
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const settle = () => new Promise<void>((resolve) => setTimeout(resolve));

  /** Signs in and answers the first favorites load. */
  async function signIn(saved: Favorite[] = [], user: User = USER): Promise<Favorites> {
    const favorites = TestBed.inject(Favorites);
    TestBed.inject(Auth).refresh().subscribe();
    http.expectOne('/api/me').flush({ data: user });
    TestBed.tick();
    http.expectOne('/api/favorites').flush({ data: saved });
    await settle();
    return favorites;
  }

  function signOut(): void {
    TestBed.inject(Favorites);
    TestBed.inject(Auth).refresh().subscribe();
    http
      .expectOne('/api/me')
      .flush({ error: { code: 'UNAUTHENTICATED', message: '' } }, { status: 401, statusText: '' });
    TestBed.tick();
  }

  // Protects against: a bookmark that waits for the server, or a save that never reaches it.
  it('fills the bookmark at once, then saves the plant', async () => {
    const favorites = await signIn();

    favorites.toggle({ id: ROSE_ID, name: 'Dog rose' });
    expect(favorites.isSaved(ROSE_ID)).toBe(true);

    const request = http.expectOne('/api/favorites');
    expect([request.request.method, request.request.body]).toEqual(['POST', { plantId: ROSE_ID }]);
    request.flush(
      { data: favorite(ROSE_ID, 'Dog rose', 1) },
      { status: 201, statusText: 'Created' },
    );
    await settle();

    expect(favorites.saved().map((f) => f.plant.name)).toEqual(['Dog rose']);
  });

  // Protects against: a bookmark that stays filled after the save failed.
  it('puts the bookmark back and says why when saving fails', async () => {
    const favorites = await signIn();

    favorites.toggle({ id: ROSE_ID, name: 'Dog rose' });
    http
      .expectOne('/api/favorites')
      .flush(
        { error: { code: 'INTERNAL_ERROR', message: 'Something went wrong.' } },
        { status: 500, statusText: '' },
      );
    await settle();
    http.expectOne('/api/favorites').flush({ data: [] });
    await settle();

    expect(favorites.isSaved(ROSE_ID)).toBe(false);
    expect(TestBed.inject(Snackbar).message()?.text).toBe('Something went wrong.');
  });

  // Protects against: an unconfirmed account failing to save without saying what to do (D-041).
  it('asks to confirm the email address, with a way to resend it', async () => {
    const favorites = await signIn([], { ...USER, emailConfirmed: false });

    favorites.toggle({ id: ROSE_ID, name: 'Dog rose' });
    http
      .expectOne('/api/favorites')
      .flush(
        { error: { code: 'EMAIL_NOT_CONFIRMED', message: '' } },
        { status: 403, statusText: '' },
      );
    await settle();
    http.expectOne('/api/favorites').flush({ data: [] });
    await settle();

    const message = TestBed.inject(Snackbar).message()!;
    expect([message.text, message.action?.label]).toEqual([
      'Confirm your email address to save plants.',
      'Resend email',
    ]);
    message.action!.run();
    http.expectOne('/api/auth/resend-confirmation').flush(null, { status: 202, statusText: '' });
    expect(TestBed.inject(Snackbar).message()?.text).toBe('We sent a new confirmation email.');
  });

  // Protects against: a quick save-then-remove reaching the server out of order and leaving the plant saved.
  it('sends quick clicks for one plant in order', async () => {
    const favorites = await signIn();

    favorites.toggle({ id: ROSE_ID, name: 'Dog rose' });
    favorites.toggle({ id: ROSE_ID, name: 'Dog rose' });
    expect(favorites.isSaved(ROSE_ID)).toBe(false);

    http
      .expectOne({ method: 'POST', url: '/api/favorites' })
      .flush({ data: favorite(ROSE_ID, 'Dog rose', 1) });
    await settle();
    http
      .expectOne({ method: 'DELETE', url: '/api/favorites/f-Dog rose' })
      .flush(null, { status: 204, statusText: '' });
    await settle();

    expect(favorites.isSaved(ROSE_ID)).toBe(false);
  });

  // Protects against: losing the user's save when they have to sign in first (PRD §19).
  it('asks a signed-out user to sign in, then saves the plant after signing in', async () => {
    signOut();
    const fixture = TestBed.createComponent(CardHost);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    const router = TestBed.inject(Router);

    host.querySelector<HTMLButtonElement>('app-bookmark-button button')!.click();
    await fixture.whenStable();

    const dialog = host.querySelector('dialog')!;
    expect(dialog.hasAttribute('open')).toBe(true);
    expect(dialog.querySelector('h2')!.textContent).toBe('Save this plant');
    expect(router.url).toBe('/'); // The bookmark did not open the plant.

    Array.from(dialog.querySelectorAll('button'))
      .find((b) => b.textContent === 'Sign in')!
      .click();
    await fixture.whenStable();
    expect(router.url).toBe('/login?returnUrl=%2F');
    expect(dialog.hasAttribute('open')).toBe(false);

    await signIn();
    const save = http.expectOne({ method: 'POST', url: '/api/favorites' });
    expect(save.request.body).toEqual({ plantId: ROSE_ID });
    save.flush({ data: favorite(ROSE_ID, 'Dog rose', 1) }, { status: 201, statusText: '' });
    await fixture.whenStable();

    expect(host.querySelector('app-bookmark-button button')!.getAttribute('aria-pressed')).toBe(
      'true',
    );
    expect(sessionStorage.length).toBe(0);
  });

  // Protects against: Cancel still saving the plant later.
  it('forgets the plant when the prompt is cancelled', async () => {
    signOut();
    const fixture = TestBed.createComponent(CardHost);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;

    host.querySelector<HTMLButtonElement>('app-bookmark-button button')!.click();
    await fixture.whenStable();
    host.querySelector<HTMLButtonElement>('dialog .cancel')!.click();
    await fixture.whenStable();

    expect(host.querySelector('dialog')!.hasAttribute('open')).toBe(false);
    await signIn();
    http.expectNone({ method: 'POST', url: '/api/favorites' });
  });

  describe('page', () => {
    async function openPage(): Promise<{ harness: RouterTestingHarness; page: HTMLElement }> {
      const harness = await RouterTestingHarness.create();
      await harness.navigateByUrl('/favorites');
      await harness.fixture.whenStable();
      return { harness, page: harness.routeNativeElement as HTMLElement };
    }

    // Protects against: removing a favorite with no way back, or Undo putting it somewhere else (PRD §18).
    it('removes a card at once and Undo puts it back in its place', async () => {
      await signIn([
        favorite(ROSE_ID, 'Dog rose', 1),
        favorite(FERN_ID, 'Fern', 2),
        favorite(MINT_ID, 'Mint', 3),
      ]);
      const { harness, page } = await openPage();
      http.expectOne('/api/favorites').flush({
        data: [
          favorite(ROSE_ID, 'Dog rose', 1),
          favorite(FERN_ID, 'Fern', 2),
          favorite(MINT_ID, 'Mint', 3),
        ],
      });
      await harness.fixture.whenStable();
      const names = () => Array.from(page.querySelectorAll('.name')).map((n) => n.textContent);

      expect(names()).toEqual(['Dog rose', 'Fern', 'Mint']);
      const fern = page.querySelectorAll<HTMLButtonElement>('app-bookmark-button button')[1];
      expect([fern.getAttribute('aria-pressed'), fern.getAttribute('aria-label')]).toEqual([
        'true',
        'Save Fern to Favorites',
      ]);

      fern.click();
      await harness.fixture.whenStable();
      expect(names()).toEqual(['Dog rose', 'Mint']);
      http
        .expectOne({ method: 'DELETE', url: '/api/favorites/f-Fern' })
        .flush(null, { status: 204, statusText: '' });
      await settle();

      const message = TestBed.inject(Snackbar).message()!;
      expect([message.text, message.action?.label]).toEqual(['Removed from Favorites.', 'Undo']);
      message.action!.run();
      await harness.fixture.whenStable();
      expect(names()).toEqual(['Dog rose', 'Fern', 'Mint']);
      const restore = http.expectOne({ method: 'POST', url: '/api/favorites' });
      expect(restore.request.body).toEqual({ plantId: FERN_ID, order: 2 });
      restore.flush(
        { data: { ...favorite(FERN_ID, 'Fern', 2), favoriteId: 'f-Fern-2' } },
        { status: 201, statusText: '' },
      );
    });

    // Protects against: an empty Favorites page with no way forward, or decorative images that act as links.
    it('shows three decorative plants and Explore Plants when nothing is saved', async () => {
      await signIn();
      const { harness, page } = await openPage();
      http.expectOne('/api/favorites').flush({ data: [] });
      await harness.fixture.whenStable();
      const cards = ['a', 'b', 'c', 'd'].map((id) => ({
        id,
        name: id,
        plantGroup: null,
        imageUrl: `/media/${id}.webp`,
      }));
      http
        .expectOne((r) => r.url === '/api/explore')
        .flush({ data: cards, pagination: { page: 1, pageSize: 20, hasNext: false } });
      await harness.fixture.whenStable();

      const decorations = page.querySelector('.decorations')!;
      expect(decorations.getAttribute('aria-hidden')).toBe('true');
      expect(decorations.querySelectorAll('img').length).toBe(3);
      expect(decorations.querySelectorAll('a').length).toBe(0);
      const explore = page.querySelector<HTMLAnchorElement>('a.action')!;
      expect([explore.textContent, explore.getAttribute('href')]).toEqual([
        'Explore Plants →',
        '/explore',
      ]);
    });

    it('asks a signed-out user to sign in', async () => {
      signOut();
      const { page } = await openPage();

      expect(page.querySelector('a.action')!.getAttribute('href')).toBe(
        '/login?returnUrl=%2Ffavorites',
      );
    });
  });
});
