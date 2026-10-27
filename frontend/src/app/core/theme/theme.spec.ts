import { TestBed } from '@angular/core/testing';
import { parseStoredTheme, Theme, THEME_STORAGE_KEY } from './theme';

/** A controllable matchMedia: whether the device prefers dark, whether it asks for reduced motion. */
function stubMedia(options: { dark?: boolean; reducedMotion?: boolean } = {}) {
  let dark = options.dark ?? false;
  const listeners: ((event: { matches: boolean }) => void)[] = [];
  vi.stubGlobal('matchMedia', (query: string) => ({
    get matches() {
      return query.includes('prefers-color-scheme: dark')
        ? dark
        : query.includes('prefers-reduced-motion: reduce')
          ? (options.reducedMotion ?? false)
          : false;
    },
    media: query,
    addEventListener: (_: string, listener: (event: { matches: boolean }) => void) => {
      if (query.includes('prefers-color-scheme')) listeners.push(listener);
    },
    removeEventListener: () => {},
  }));
  return {
    setDeviceDark(value: boolean) {
      dark = value;
      listeners.forEach((listener) => listener({ matches: value }));
    },
  };
}

const themeColor = () =>
  document.querySelector('meta[name="theme-color"]')?.getAttribute('content');

describe('Theme', () => {
  beforeEach(() => {
    localStorage.clear();
    delete document.documentElement.dataset['theme'];
    document.documentElement.classList.remove('theme-transition');
    document.head.querySelector('meta[name="theme-color"]')?.remove();
    const meta = document.createElement('meta');
    meta.name = 'theme-color';
    meta.content = '#F4F1EA';
    document.head.append(meta);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    vi.useRealTimers();
    localStorage.clear();
  });

  // Protects against: a tampered or old stored value picking a theme that does not exist.
  it('accepts only light or dark as a stored choice', () => {
    expect(parseStoredTheme('dark')).toBe('dark');
    expect(parseStoredTheme('light')).toBe('light');
    expect(parseStoredTheme('purple')).toBeNull();
    expect(parseStoredTheme(null)).toBeNull();
  });

  // Protects against: the app disagreeing with the theme index.html already painted (a flash).
  it('starts from the theme index.html chose', () => {
    stubMedia();
    document.documentElement.dataset['theme'] = 'dark';

    expect(TestBed.inject(Theme).theme()).toBe('dark');
  });

  // Protects against: a switch that does not stick, or browser chrome left in the old colour.
  it('switches, remembers the choice, and updates the page', () => {
    stubMedia();
    const theme = TestBed.inject(Theme);

    theme.toggle();

    expect(theme.theme()).toBe('dark');
    expect(document.documentElement.dataset['theme']).toBe('dark');
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark');
    expect(themeColor()).toBe('#0E1612');

    theme.toggle();
    expect(themeColor()).toBe('#F4F1EA');
  });

  // Protects against: private mode or blocked storage breaking the switch.
  it('still switches for this visit when storage is blocked', () => {
    stubMedia();
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    const theme = TestBed.inject(Theme);

    expect(() => theme.toggle()).not.toThrow();
    expect(theme.theme()).toBe('dark');
    expect(document.documentElement.dataset['theme']).toBe('dark');
  });

  // Protects against: ignoring the device setting before any choice, or overriding a choice afterwards.
  it('follows the device until the visitor chooses', () => {
    const media = stubMedia();
    const theme = TestBed.inject(Theme);
    expect(theme.theme()).toBe('light');

    media.setDeviceDark(true);
    expect(theme.theme()).toBe('dark');

    theme.set('light');
    media.setDeviceDark(false);
    media.setDeviceDark(true);
    expect(theme.theme()).toBe('light');
  });

  // Protects against: a choice saved on an earlier visit being ignored.
  it('uses a stored choice over the device setting', () => {
    stubMedia({ dark: true });
    localStorage.setItem(THEME_STORAGE_KEY, 'light');

    expect(TestBed.inject(Theme).theme()).toBe('light');
  });

  // Protects against: a permanent transition on every element (slow), or a cross-fade for reduced motion.
  it('cross-fades only briefly, and not when motion is reduced', () => {
    vi.useFakeTimers();
    stubMedia();
    const theme = TestBed.inject(Theme);

    theme.toggle();
    expect(document.documentElement.classList).toContain('theme-transition');
    vi.advanceTimersByTime(350);
    expect(document.documentElement.classList).not.toContain('theme-transition');

    TestBed.resetTestingModule();
    stubMedia({ reducedMotion: true });
    TestBed.inject(Theme).toggle();
    expect(document.documentElement.classList).not.toContain('theme-transition');
  });
});
