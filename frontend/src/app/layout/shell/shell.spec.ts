import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { routes } from '../../app.routes';
import { Shell } from './shell';

describe('Shell', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Shell],
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  // Protects against: missing or renamed primary destinations (UX §3, D-036).
  it('shows Identify, Explore and Favorites in the desktop sidebar', async () => {
    const fixture = TestBed.createComponent(Shell);
    await fixture.whenStable();
    const sidebar = (fixture.nativeElement as HTMLElement).querySelector('aside')!;

    const labels = Array.from(sidebar.querySelectorAll('nav a span:not(.nav-icon)')).map(
      (s) => s.textContent,
    );

    expect(labels).toEqual(['Identify', 'Explore', 'Favorites']);
  });

  // Protects against: no way to switch themes on desktop or on a phone (design 2026-10-26).
  it('offers the theme switch in the sidebar and the top bar', async () => {
    const fixture = TestBed.createComponent(Shell);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const named = (root: Element) =>
      Array.from(root.querySelectorAll('button')).some((b) =>
        /^Switch to (dark|light) theme$/.test(b.getAttribute('aria-label') ?? ''),
      );

    expect(named(element.querySelector('aside')!)).toBe(true);
    expect(named(element.querySelector('app-top-bar')!)).toBe(true);
  });

  // Protects against: the collapse control not working or not announcing its state.
  it('collapses and expands the desktop sidebar', async () => {
    const fixture = TestBed.createComponent(Shell);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const aside = element.querySelector('aside')!;
    const toggle = aside.querySelector<HTMLButtonElement>('.collapse-toggle')!;

    expect(toggle.getAttribute('aria-expanded')).toBe('true');

    toggle.click();
    await fixture.whenStable();

    expect(aside.classList).toContain('collapsed');
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    expect(toggle.getAttribute('aria-label')).toBe('Expand sidebar');
    // Collapsed links keep an accessible name even though the visible label is hidden.
    expect(aside.querySelector('nav a')?.getAttribute('aria-label')).toBe('Identify');

    toggle.click();
    await fixture.whenStable();

    expect(aside.classList).not.toContain('collapsed');
    expect(toggle.getAttribute('aria-expanded')).toBe('true');
  });
});
