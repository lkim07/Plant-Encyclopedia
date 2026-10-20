import { DestroyRef, Directive, ElementRef, inject, output } from '@angular/core';

/**
 * Emits `visible` when the element comes within 400px of the viewport: the trigger for infinite scroll.
 * Put it on a sentinel below a list that exists only while another page can be loaded; each new sentinel
 * observes again. Does nothing where IntersectionObserver is unavailable (a Load more button covers that).
 */
@Directive({ selector: '[appWhenVisible]' })
export class WhenVisible {
  readonly visible = output<void>();

  constructor() {
    if (typeof IntersectionObserver === 'undefined') {
      return;
    }
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          this.visible.emit();
        }
      },
      { rootMargin: '400px 0px' },
    );
    observer.observe(inject(ElementRef<HTMLElement>).nativeElement);
    inject(DestroyRef).onDestroy(() => observer.disconnect());
  }
}
