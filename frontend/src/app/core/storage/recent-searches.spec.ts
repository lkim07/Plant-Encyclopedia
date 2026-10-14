import { TestBed } from '@angular/core/testing';
import { RecentSearches } from './recent-searches';

const KEY = 'plant-encyclopedia.recent-searches';

describe('RecentSearches', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

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
});
