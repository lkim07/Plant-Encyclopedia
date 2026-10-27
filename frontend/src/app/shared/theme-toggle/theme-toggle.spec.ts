import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ThemeToggle } from './theme-toggle';

@Component({
  imports: [ThemeToggle],
  template: `<app-theme-toggle [showLabel]="showLabel()" />`,
})
class Host {
  readonly showLabel = signal(false);
}

describe('ThemeToggle', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.dataset['theme'] = 'light';
  });

  afterEach(() => {
    localStorage.clear();
    document.documentElement.dataset['theme'] = 'light';
  });

  async function render(showLabel = false) {
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.showLabel.set(showLabel);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    return { fixture, button: () => element.querySelector('button')! };
  }

  // Protects against: a switch that does not say what it will do, or does nothing (design: named for screen readers).
  it('names the theme it switches to, and switches', async () => {
    const { fixture, button } = await render();
    expect(button().getAttribute('aria-label')).toBe('Switch to dark theme');
    expect(button().querySelector('app-icon')!.getAttribute('data-icon')).toBe('moon');

    button().click();
    await fixture.whenStable();

    expect(document.documentElement.dataset['theme']).toBe('dark');
    expect(button().getAttribute('aria-label')).toBe('Switch to light theme');
    expect(button().querySelector('app-icon')!.getAttribute('data-icon')).toBe('sun');
  });

  // Protects against: an unlabeled icon in the expanded sidebar.
  it('shows a visible label when asked', async () => {
    const { button } = await render(true);

    expect(button().textContent!.trim()).toBe('Dark theme');
  });

  // Protects against: the icon stacked above its label in the sidebar (shared .icon-button is a grid; QA pass).
  it('lays the icon and label out in a row like the nav items', async () => {
    const { button } = await render(true);

    expect(getComputedStyle(button()).display).toBe('flex');
  });
});
