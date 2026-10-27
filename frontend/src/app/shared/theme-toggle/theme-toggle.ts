import { Component, computed, inject, input } from '@angular/core';
import { Theme } from '../../core/theme/theme';
import { Icon } from '../icons/icon';

/**
 * Switches between the light and dark themes (design 2026-10-26). The accessible name says which theme it switches
 * to; the icon shows it (moon for dark, sun for light). In the expanded sidebar a visible label goes with it.
 */
@Component({
  selector: 'app-theme-toggle',
  imports: [Icon],
  template: `
    <button
      type="button"
      class="icon-button theme-toggle"
      [class.labelled]="showLabel()"
      [attr.aria-label]="'Switch to ' + next() + ' theme'"
      (click)="theme.toggle()"
    >
      <app-icon [name]="next() === 'dark' ? 'moon' : 'sun'" />
      @if (showLabel()) {
        <span aria-hidden="true">{{ next() === 'dark' ? 'Dark theme' : 'Light theme' }}</span>
      }
    </button>
  `,
  styles: `
    :host {
      display: inline-flex;
    }

    /* In the expanded sidebar it looks like a nav item: icon and label in a row (.icon-button is a grid). */
    .labelled {
      display: flex;
      align-items: center;
      width: 100%;
      justify-content: flex-start;
      gap: var(--space-3);
      padding: var(--space-2);
      white-space: nowrap;
      transition: background-color var(--transition-fast);
    }

    .labelled:hover {
      background: var(--color-surface-strong);
    }

    /* The same icon size as the nav items, so the labels line up. */
    .labelled app-icon {
      width: 1.75rem;
      height: 1.75rem;
    }
  `,
})
export class ThemeToggle {
  protected readonly theme = inject(Theme);

  /** Show "Dark theme" / "Light theme" next to the icon (the expanded sidebar). */
  readonly showLabel = input(false);

  protected readonly next = computed(() => (this.theme.theme() === 'dark' ? 'light' : 'dark'));
}
