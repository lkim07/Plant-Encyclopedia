import { Component, inject, input, output } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { Auth } from '../../core/auth/auth';
import { Icon, IconName } from '../../shared/icons/icon';

interface NavItem {
  readonly path: string;
  readonly label: string;
  readonly icon: IconName;
}

/**
 * Destination navigation (UX §3, D-036) and the account entry (D-070): Sign in, or the signed-in user with Sign out.
 * Used by the desktop sidebar and the mobile drawer.
 */
@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive, Icon],
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.css',
})
export class Sidebar {
  protected readonly auth = inject(Auth);
  private readonly router = inject(Router);

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

  /** The first letter of the name, for the account circle. */
  protected initial(name: string): string {
    return [...name.trim()][0]?.toUpperCase() ?? '?';
  }

  protected signOut(): void {
    this.navigate.emit();
    this.auth.logout().subscribe({ next: () => void this.router.navigateByUrl('/explore') });
  }
}
