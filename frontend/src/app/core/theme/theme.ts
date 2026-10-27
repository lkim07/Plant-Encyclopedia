import { Injectable, signal } from '@angular/core';
import { motionAllowed } from '../../shared/motion';

export type ThemeName = 'light' | 'dark';

/** Where the visitor's choice is kept. index.html reads the same key before the app starts. */
export const THEME_STORAGE_KEY = 'plant-encyclopedia.theme';

/** The browser chrome colour per theme: each theme's page ground. */
const THEME_COLORS: Record<ThemeName, string> = { light: '#F4F1EA', dark: '#0E1612' };

/** How long colours cross-fade when the theme switches (styles.css: html.theme-transition). */
const CROSS_FADE_MS = 350;

const DARK_QUERY = '(prefers-color-scheme: dark)';

/** A stored value is untrusted: only "light" or "dark" count as a choice. */
export function parseStoredTheme(value: string | null): ThemeName | null {
  return value === 'light' || value === 'dark' ? value : null;
}

/**
 * The light ("Greenhouse Daylight") or dark ("Night Greenhouse") theme (design 2026-10-26). index.html sets
 * `data-theme` on <html> before the app starts, so the first paint has the right colours; this service takes over:
 * it follows the device setting until the visitor chooses, then keeps the choice in this browser. Storage failures
 * (private mode, blocked site data) only mean the choice lasts for this visit.
 */
@Injectable({ providedIn: 'root' })
export class Theme {
  private readonly root = document.documentElement;
  private readonly current = signal<ThemeName>(this.initial());
  private chosen = readStored() !== null;
  private fadeTimer?: ReturnType<typeof setTimeout>;

  readonly theme = this.current.asReadonly();

  constructor() {
    this.apply(this.current());
    if (typeof matchMedia === 'function') {
      matchMedia(DARK_QUERY).addEventListener?.('change', (event) => {
        if (!this.chosen) {
          this.show(event.matches ? 'dark' : 'light');
        }
      });
    }
  }

  toggle(): void {
    this.set(this.current() === 'dark' ? 'light' : 'dark');
  }

  /** The visitor's choice: shown now and remembered. */
  set(theme: ThemeName): void {
    this.chosen = true;
    try {
      localStorage.setItem(THEME_STORAGE_KEY, theme);
    } catch {
      // Storage blocked: the choice lasts for this visit.
    }
    this.show(theme);
  }

  private show(theme: ThemeName): void {
    if (motionAllowed()) {
      this.root.classList.add('theme-transition');
      clearTimeout(this.fadeTimer);
      this.fadeTimer = setTimeout(
        () => this.root.classList.remove('theme-transition'),
        CROSS_FADE_MS,
      );
    }
    this.current.set(theme);
    this.apply(theme);
  }

  private apply(theme: ThemeName): void {
    this.root.dataset['theme'] = theme;
    document
      .querySelector('meta[name="theme-color"]')
      ?.setAttribute('content', THEME_COLORS[theme]);
  }

  private initial(): ThemeName {
    const painted = parseStoredTheme(this.root.dataset['theme'] ?? null);
    return (
      painted ??
      readStored() ??
      (typeof matchMedia === 'function' && matchMedia(DARK_QUERY).matches ? 'dark' : 'light')
    );
  }
}

function readStored(): ThemeName | null {
  try {
    return parseStoredTheme(localStorage.getItem(THEME_STORAGE_KEY));
  } catch {
    return null;
  }
}
