import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { routes } from '../../app.routes';
import { Shell } from './shell';

describe('Shell', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Shell],
      providers: [provideRouter(routes)],
    }).compileComponents();
  });

  // Protects against: missing or renamed primary destinations (UX §3, D-036).
  it('shows Identify, Explore and Favorites in the desktop sidebar', async () => {
    const fixture = TestBed.createComponent(Shell);
    await fixture.whenStable();
    const sidebar = (fixture.nativeElement as HTMLElement).querySelector('aside')!;

    const labels = Array.from(sidebar.querySelectorAll('nav a span:not(.nav-icon)')).map((s) => s.textContent);

    expect(labels).toEqual(['Identify', 'Explore', 'Favorites']);
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
