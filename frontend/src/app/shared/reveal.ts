import { DestroyRef, Directive, ElementRef, inject } from '@angular/core';
import { motionAllowed } from './motion';

/**
 * Fades and lifts the element in the first time it comes into view (styles: .reveal in styles.css). Items in a
 * list are staggered by their position in a row of four, so a new page from infinite scroll animates on its own.
 * Nothing is hidden when motion is off or IntersectionObserver is missing: the element simply stays visible.
 */
@Directive({ selector: '[appReveal]' })
export class Reveal {
  constructor() {
    if (!motionAllowed() || typeof IntersectionObserver === 'undefined') {
      return;
    }

    const element: HTMLElement = inject(ElementRef).nativeElement;
    element.classList.add('reveal');

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          // Measured now, when the element is in the page (list items are attached after creation).
          const item = element.closest('li') ?? element;
          const index = item.parentElement
            ? Array.prototype.indexOf.call(item.parentElement.children, item)
            : 0;
          element.style.setProperty('--reveal-delay', `${(index % 4) * 70}ms`);
          element.classList.add('revealed');
          observer.disconnect();
        }
      },
      { rootMargin: '0px 0px -5% 0px' },
    );
    observer.observe(element);
    inject(DestroyRef).onDestroy(() => observer.disconnect());
  }
}
