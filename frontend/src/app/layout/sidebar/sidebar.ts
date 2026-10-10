import { Component, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

interface NavItem {
  readonly path: string;
  readonly label: string;
  readonly icon: string;
}

/** Destination navigation (UX §3, D-036). Used by the desktop sidebar and the mobile drawer. */
@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.css',
})
export class Sidebar {
  readonly collapsed = input(false);
  readonly showCollapseToggle = input(false);

  readonly toggleCollapsed = output<void>();
  /** Emitted when a destination is chosen, so the mobile drawer can close. */
  readonly navigate = output<void>();

  protected readonly items: readonly NavItem[] = [
    { path: '/identify', label: 'Identify', icon: '📷' },
    { path: '/explore', label: 'Explore', icon: '🔍' },
    { path: '/favorites', label: 'Favorites', icon: '🔖' },
  ];
}
