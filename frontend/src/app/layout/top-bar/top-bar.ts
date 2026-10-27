import { Component, inject, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Auth } from '../../core/auth/auth';
import { Icon } from '../../shared/icons/icon';
import { ThemeToggle } from '../../shared/theme-toggle/theme-toggle';

/** Mobile top application bar: menu, title, theme switch, account (UX §4, D-070). */
@Component({
  selector: 'app-top-bar',
  imports: [Icon, RouterLink, ThemeToggle],
  templateUrl: './top-bar.html',
  styleUrl: './top-bar.css',
})
export class TopBar {
  protected readonly auth = inject(Auth);

  readonly menuOpen = output<void>();

  protected initial(name: string): string {
    return [...name.trim()][0]?.toUpperCase() ?? '?';
  }
}
