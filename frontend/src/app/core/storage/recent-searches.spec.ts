import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Auth } from '../auth/auth';
import { RecentSearches } from './recent-searches';

const KEY = 'plant-encyclopedia.recent-searches';
const SEARCHES = '/api/history/searches';

describe('RecentSearches', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => {
    http.verify();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  const settle = () => new Promise<void>((resolve) => setTimeout(resolve));

  const create = () => TestBed.inject(RecentSearches);

  // Protects against: duplicates, wrong order, or an unbounded list (UX §20).
  it('keeps the newest first, moves repeats up (ignoring case), and caps the list at 10', () => {
    const recent = create();
    for (let i = 1; i <= 11; i++) {
      recent.add(`plant ${i}`);
    }
    recent.add('  PLANT 5  ');

    expect(recent.items()).toEqual([
      'PLANT 5',
      'plant 11',
      'plant 10',
      'plant 9',
      'plant 8',
      'plant 7',
      'plant 6',
      'plant 4',
      'plant 3',
      'plant 2',
    ]);
    expect(JSON.parse(localStorage.getItem(KEY)!)).toEqual(recent.items());
  });

  // Protects against: the × button not removing an entry.
  it('removes an entry', () => {
    const recent = create();
    recent.add('rose');
    recent.add('peony');

    recent.remove('rose');

    expect(recent.items()).toEqual(['peony']);
    expect(JSON.parse(localStorage.getItem(KEY)!)).toEqual(['peony']);
  });

  // Protects against: tampered or corrupted storage breaking the page or showing junk.
  it('ignores corrupted or unexpected stored data', () => {
    localStorage.setItem(
      KEY,
      JSON.stringify(['rose', 42, null, '', 'ROSE', 'x'.repeat(101), { a: 1 }, ' tulip ']),
    );
    expect(create().items()).toEqual(['rose', 'tulip']);

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    localStorage.setItem(KEY, '{not json');
    expect(create().items()).toEqual([]);
  });

  // Protects against: blocked storage (private mode) breaking search.
  it('works in memory when storage is unavailable', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });

    const recent = create();
    recent.add('rose');

    expect(recent.items()).toEqual(['rose']);
  });
  describe('signed in', () => {
    async function signIn(saved: { searchId: string; query: string }[]): Promise<RecentSearches> {
      const recent = create();
      TestBed.inject(Auth).refresh().subscribe();
      http.expectOne('/api/me').flush({
        data: { id: 'u1', displayName: 'Rose Lover', email: null, emailConfirmed: true },
      });
      TestBed.tick();
      await settle();
      http.expectOne(SEARCHES).flush({ data: saved });
      await settle();
      return recent;
    }

    // Protects against: a signed-in user's searches staying on this device instead of following their account.
    it('shows the server history and saves new searches there, not in this browser', async () => {
      localStorage.setItem(KEY, JSON.stringify(['local only']));
      const recent = await signIn([{ searchId: 's1', query: 'peony' }]);
      expect(recent.items()).toEqual(['peony']);

      recent.add('Rose');
      expect(recent.items()).toEqual(['Rose', 'peony']);
      await settle();
      const save = http.expectOne({ method: 'POST', url: SEARCHES });
      expect(save.request.body).toEqual({ query: 'Rose' });
      save.flush({ data: { searchId: 's2', query: 'Rose', searchedAt: '2026-10-25T00:00:00Z' } });
      await settle();

      expect(JSON.parse(localStorage.getItem(KEY)!)).toEqual(['local only']);
    });

    // Protects against: a removed search coming back, including one removed before its save finished (UX §20).
    it('removes searches on the server, also right after adding them', async () => {
      const recent = await signIn([{ searchId: 's1', query: 'peony' }]);

      recent.remove('peony');
      recent.add('fern');
      recent.remove('fern');
      expect(recent.items()).toEqual([]);

      await settle();
      http
        .expectOne({ method: 'DELETE', url: `${SEARCHES}/s1` })
        .flush(null, { status: 204, statusText: '' });
      await settle();
      http
        .expectOne({ method: 'POST', url: SEARCHES })
        .flush({ data: { searchId: 's2', query: 'fern', searchedAt: '2026-10-25T00:00:00Z' } });
      await settle();
      http
        .expectOne({ method: 'DELETE', url: `${SEARCHES}/s2` })
        .flush(null, { status: 204, statusText: '' });
      await settle();
    });

    // Protects against: the screen showing a list the server did not keep.
    it('loads the list again when a request fails', async () => {
      const recent = await signIn([{ searchId: 's1', query: 'peony' }]);

      recent.remove('peony');
      await settle();
      http
        .expectOne({ method: 'DELETE', url: `${SEARCHES}/s1` })
        .flush(null, { status: 500, statusText: '' });
      await settle();
      http.expectOne(SEARCHES).flush({ data: [{ searchId: 's1', query: 'peony' }] });
      await settle();

      expect(recent.items()).toEqual(['peony']);
    });

    // Protects against: one account's searches showing after signing out on a shared device.
    it("goes back to this browser's list after signing out", async () => {
      localStorage.setItem(KEY, JSON.stringify(['local only']));
      const recent = await signIn([{ searchId: 's1', query: 'peony' }]);

      TestBed.inject(Auth).refresh().subscribe();
      http
        .expectOne('/api/me')
        .flush(
          { error: { code: 'UNAUTHENTICATED', message: '' } },
          { status: 401, statusText: '' },
        );
      TestBed.tick();

      expect(recent.items()).toEqual(['local only']);
    });
  });
});
