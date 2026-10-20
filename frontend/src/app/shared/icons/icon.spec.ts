import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Icon, IconName } from './icon';

@Component({
  imports: [Icon],
  template: `<button type="button" aria-label="Search"><app-icon [name]="name()" /></button>`,
})
class Host {
  readonly name = signal<IconName>('search');
}

describe('Icon', () => {
  // Protects against: icons announced by screen readers next to their control's own label, or icons drawn
  // with a fill instead of the outlined style.
  it('draws an outlined, decorative SVG for the named icon', async () => {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    const icon = (fixture.nativeElement as HTMLElement).querySelector('app-icon')!;
    const svg = icon.querySelector('svg')!;

    expect(icon.getAttribute('aria-hidden')).toBe('true');
    expect(icon.getAttribute('data-icon')).toBe('search');
    expect([svg.getAttribute('fill'), svg.getAttribute('stroke')]).toEqual([
      'none',
      'currentColor',
    ]);
    expect(svg.querySelectorAll('path').length).toBe(2);

    fixture.componentInstance.name.set('bookmark');
    await fixture.whenStable();
    expect(icon.getAttribute('data-icon')).toBe('bookmark');
    expect(svg.querySelectorAll('path').length).toBe(1);
  });
});
