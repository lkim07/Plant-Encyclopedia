import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Outlined icons drawn for this project in one style: a 24-pixel grid, 1.75 stroke, round caps and joins, no
 * fill. They take the brown icon colour (--color-icon) unless the parent sets `color`. Always decorative:
 * the control next to or around the icon carries the accessible name.
 */
const PATHS = {
  camera: [
    'M4 8h3l2-3h6l2 3h3a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V9a1 1 0 0 1 1-1z',
    'M12 16.5a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7z',
  ],
  compass: ['M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18z', 'M15.5 8.5l-2 5-5 2 2-5z'],
  bookmark: ['M6.5 4h11v16l-5.5-3.8L6.5 20z'],
  'log-out': ['M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4', 'M16 17l5-5-5-5', 'M21 12H9'],
  user: ['M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8z', 'M4.5 20c0-3.6 3.4-5.5 7.5-5.5s7.5 1.9 7.5 5.5'],
  leaf: ['M11 20a7 7 0 0 1-7-7c0-5.5 4.5-9 16-9 0 10.5-3.5 16-9 16z', 'M4.5 19.5C8 15 11 12 15 9'],
  search: ['M11 18a7 7 0 1 0 0-14 7 7 0 0 0 0 14z', 'M20 20l-4-4'],
  'arrow-left': ['M19 12H5', 'M11 18l-6-6 6-6'],
  close: ['M18 6L6 18', 'M6 6l12 12'],
  menu: ['M4 6.5h16', 'M4 12h16', 'M4 17.5h16'],
  'chevrons-left': ['M11 17l-5-5 5-5', 'M18 17l-5-5 5-5'],
  'chevrons-right': ['M13 17l5-5-5-5', 'M6 17l5-5-5-5'],
  'chevron-down': ['M6 9l6 6 6-6'],
  'chevron-up': ['M6 15l6-6 6 6'],
  sun: [
    'M12 16a4 4 0 1 0 0-8 4 4 0 0 0 0 8z',
    'M12 2.5v2M12 19.5v2M4.6 4.6L6 6M18 18l1.4 1.4M2.5 12h2M19.5 12h2M4.6 19.4L6 18M18 6l1.4-1.4',
  ],
  droplet: ['M12 3.5c3.4 4 5.8 7.3 5.8 10.2a5.8 5.8 0 0 1-11.6 0c0-2.9 2.4-6.2 5.8-10.2z'],
  thermometer: ['M14 14.6V5a2 2 0 0 0-4 0v9.6a4 4 0 1 0 4 0z', 'M12 17v-6'],
  sprout: [
    'M12 21v-8.5',
    'M12 12.5C12 8.8 9.4 6.5 5.5 6.5c0 3.7 2.6 6 6.5 6z',
    'M12 14.5c0-3.7 2.6-6 6.5-6 0 3.7-2.6 6-6.5 6z',
  ],
} as const satisfies Record<string, readonly string[]>;

export type IconName = keyof typeof PATHS;

@Component({
  selector: 'app-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'aria-hidden': 'true', '[attr.data-icon]': 'name()' },
  template: `
    <svg
      viewBox="0 0 24 24"
      [attr.fill]="filled() ? 'currentColor' : 'none'"
      stroke="currentColor"
      stroke-width="1.75"
      stroke-linecap="round"
      stroke-linejoin="round"
      focusable="false"
    >
      @for (d of paths(); track $index) {
        <path [attr.d]="d" />
      }
    </svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      width: 1.375em;
      height: 1.375em;
      flex: none;
      color: var(--icon-color, var(--color-icon));
    }

    svg {
      width: 100%;
      height: 100%;
    }
  `,
})
export class Icon {
  readonly name = input.required<IconName>();

  /** Fills the shape too, for an "on" state such as a saved bookmark. Icons are outlined otherwise. */
  readonly filled = input(false);

  protected readonly paths = computed(() => PATHS[this.name()]);
}
