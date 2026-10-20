import { Component, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { Icon, IconName } from '../../shared/icons/icon';

interface NavItem {
  readonly path: string;
  readonly label: string;
  readonly icon: IconName;
}

/** Destination navigation (UX §3, D-036). Used by the desktop sidebar and the mobile drawer. */
@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive, Icon],
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
    { path: '/identify', label: 'Identify', icon: 'camera' },
    { path: '/explore', label: 'Explore', icon: 'compass' },
    { path: '/favorites', label: 'Favorites', icon: 'bookmark' },
  ];
}
