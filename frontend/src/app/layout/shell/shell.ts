import {
  afterNextRender,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Sidebar } from '../sidebar/sidebar';
import { TopBar } from '../top-bar/top-bar';
import { Icon } from '../../shared/icons/icon';
import { isDesktopPointer, motionAllowed } from '../../shared/motion';

/**
 * Application shell.
 * Desktop: collapsible sidebar. Mobile: top bar plus a slide-in drawer (UX §3–4, D-039).
 * The drawer is a native modal <dialog>, which provides focus trapping and Esc-to-close.
 * On desktop with a mouse or trackpad, the whole site scrolls smoothly (Lenis, loaded only there); never on
 * touch screens or with reduced motion (UX §51–52).
 */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, Sidebar, TopBar, Icon],
  templateUrl: './shell.html',
  styleUrl: './shell.css',
})
export class Shell {
  protected readonly collapsed = signal(false);

  private readonly drawer = viewChild.required<ElementRef<HTMLDialogElement>>('drawer');
  private readonly main = viewChild.required<ElementRef<HTMLElement>>('main');

  constructor() {
    const destroyRef = inject(DestroyRef);
    afterNextRender(async () => {
      if (!motionAllowed() || !isDesktopPointer()) {
        return;
      }
      const { default: Lenis } = await import('lenis');
      const lenis = new Lenis({ autoRaf: true, lerp: 0.12 });
      destroyRef.onDestroy(() => lenis.destroy());
    });
  }

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
