import { Component, ElementRef, signal, viewChild } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Sidebar } from '../sidebar/sidebar';
import { TopBar } from '../top-bar/top-bar';

/**
 * Application shell.
 * Desktop: collapsible sidebar. Mobile: top bar plus a slide-in drawer (UX §3–4, D-039).
 * The drawer is a native modal <dialog>, which provides focus trapping and Esc-to-close.
 */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, Sidebar, TopBar],
  templateUrl: './shell.html',
  styleUrl: './shell.css',
})
export class Shell {
  protected readonly collapsed = signal(false);

  private readonly drawer = viewChild.required<ElementRef<HTMLDialogElement>>('drawer');
  private readonly main = viewChild.required<ElementRef<HTMLElement>>('main');

  protected toggleCollapsed(): void {
    this.collapsed.update((value) => !value);
  }

  protected openDrawer(): void {
    this.drawer().nativeElement.showModal();
  }

  protected closeDrawer(): void {
    this.drawer().nativeElement.close();
  }

  /** A click whose target is the <dialog> itself landed on the backdrop, outside the panel. */
  protected onDrawerClick(event: MouseEvent): void {
    if (event.target === this.drawer().nativeElement) {
      this.closeDrawer();
    }
  }

  protected skipToMain(): void {
    this.main().nativeElement.focus();
  }
}
