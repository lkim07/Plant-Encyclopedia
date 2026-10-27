import {
  afterNextRender,
  DestroyRef,
  Directive,
  ElementRef,
  inject,
  InjectionToken,
} from '@angular/core';
import { loadGsap, motionAllowed } from './motion';

/** A running title animation. */
export interface TitleAnimation {
  readonly finished: Promise<void>;
  cancel(): void;
}

/** Moves the word elements into place; replaceable in tests. */
export type TitleAnimator = (words: readonly HTMLElement[]) => TitleAnimation;

/** GSAP: each word rises and fades in, one after another (design 2026-10-26, about 900 ms). */
export const TITLE_ANIMATOR = new InjectionToken<TitleAnimator>('TitleAnimator', {
  providedIn: 'root',
  factory: () => (words) => {
    let cancelled = false;
    let kill = () => {};
    const finished = loadGsap().then(
      ({ gsap }) =>
        new Promise<void>((resolve) => {
          if (cancelled) return resolve();
          const tween = gsap.from(words, {
            yPercent: 60,
            opacity: 0,
            stagger: 0.09,
            duration: 0.9,
            ease: 'power3.out',
            onComplete: () => resolve(),
          });
          kill = () => {
            tween.kill();
            resolve();
          };
        }),
    );
    return {
      finished,
      cancel: () => {
        cancelled = true;
        kill();
      },
    };
  },
});

/**
 * Page titles rise in word by word when they appear (design 2026-10-26). The real title is never modified, so
 * Angular's bindings keep working: an aria-hidden copy of its words is laid over it and animated while the real text
 * is transparent, then the copy is removed. If the title's text changes meanwhile, the copy goes at once. Nothing
 * happens when the visitor asks for reduced motion.
 */
@Directive({ selector: '[appTitleRise]' })
export class TitleRise {
  private readonly host: HTMLElement = inject(ElementRef).nativeElement;
  private readonly animate = inject(TITLE_ANIMATOR);
  private copy?: HTMLElement;
  private animation?: TitleAnimation;
  private watcher?: MutationObserver;

  constructor() {
    if (!motionAllowed()) {
      return;
    }
    afterNextRender(() => this.start());
    inject(DestroyRef).onDestroy(() => this.stop());
  }

  private start(): void {
    const words = (this.host.textContent ?? '').trim().split(/\s+/).filter(Boolean);
    if (words.length === 0) {
      return;
    }

    const copy = document.createElement('span');
    copy.className = 'title-rise-words';
    copy.setAttribute('aria-hidden', 'true');
    copy.style.color = getComputedStyle(this.host).color;
    const wordElements = words.map((word, index) => {
      const element = document.createElement('span');
      element.className = 'title-rise-word';
      element.textContent = word;
      copy.append(element, index < words.length - 1 ? ' ' : '');
      return element;
    });

    this.host.classList.add('title-rising');
    this.host.style.color = 'transparent';
    this.host.append(copy);
    this.copy = copy;

    // Any later change to the real title (a binding update) ends the animation.
    this.watcher = new MutationObserver(() => this.stop());
    this.watcher.observe(this.host, { childList: true, characterData: true, subtree: true });

    this.animation = this.animate(wordElements);
    // Finished or failed (GSAP could not load): the real title shows either way.
    void this.animation.finished.then(
      () => this.clear(),
      () => this.clear(),
    );
  }

  private stop(): void {
    this.animation?.cancel();
    this.clear();
  }

  private clear(): void {
    this.watcher?.disconnect();
    this.watcher = undefined;
    this.animation = undefined;
    if (this.copy) {
      this.copy.remove();
      this.copy = undefined;
      this.host.classList.remove('title-rising');
      this.host.style.color = '';
    }
  }
}
