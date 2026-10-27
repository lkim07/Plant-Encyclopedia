import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TITLE_ANIMATOR, TitleAnimation, TitleRise } from './title-rise';

@Component({
  imports: [TitleRise],
  template: `<h1 appTitleRise>{{ title() }}</h1>`,
})
class Host {
  readonly title = signal('Possible matches');
}

/** Stands in for GSAP: records the words it was given and finishes when told to. */
class FakeAnimator {
  words: string[] = [];
  cancelled = 0;
  private finish!: () => void;

  readonly animate = (words: readonly HTMLElement[]): TitleAnimation => {
    this.words = words.map((w) => w.textContent ?? '');
    return {
      finished: new Promise<void>((resolve) => (this.finish = resolve)),
      cancel: () => this.cancelled++,
    };
  };

  done(): void {
    this.finish();
  }
}

function stubReducedMotion(reduce: boolean) {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: query.includes('prefers-reduced-motion: reduce') ? reduce : false,
    media: query,
    addEventListener: () => {},
    removeEventListener: () => {},
  }));
}

describe('TitleRise', () => {
  let animator: FakeAnimator;

  beforeEach(() => {
    animator = new FakeAnimator();
    TestBed.configureTestingModule({
      providers: [{ provide: TITLE_ANIMATOR, useValue: animator.animate }],
    });
  });

  afterEach(() => vi.unstubAllGlobals());

  const settle = () => new Promise<void>((resolve) => setTimeout(resolve));

  async function render() {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    await settle();
    const h1 = (fixture.nativeElement as HTMLElement).querySelector('h1')!;
    const realText = () =>
      Array.from(h1.childNodes)
        .filter((n) => n.nodeType === Node.TEXT_NODE)
        .map((n) => n.textContent)
        .join('');
    return { fixture, h1, realText, copy: () => h1.querySelector('.title-rise-words') };
  }

  // Protects against: any movement for people who asked their device for reduced motion.
  it('does nothing when motion is reduced', async () => {
    stubReducedMotion(true);
    const { h1, copy } = await render();

    expect(copy()).toBeNull();
    expect(h1.style.color).toBe('');
    expect(animator.words).toEqual([]);
  });

  // Protects against: screen readers hearing the title twice, or the real title being rewritten.
  it('animates a hidden copy of the words over the untouched title', async () => {
    stubReducedMotion(false);
    const { h1, realText, copy } = await render();

    expect(animator.words).toEqual(['Possible', 'matches']);
    expect(copy()!.getAttribute('aria-hidden')).toBe('true');
    expect(realText()).toBe('Possible matches');
    expect(h1.style.color).toBe('transparent');

    animator.done();
    await settle();
    expect(copy()).toBeNull();
    expect(h1.style.color).toBe('');
  });

  // Protects against: a title that changes while rising (a reused page) showing the old text (Review Focus 4).
  it('stops and shows the new text when the title changes mid-animation', async () => {
    stubReducedMotion(false);
    const { fixture, h1, realText, copy } = await render();

    fixture.componentInstance.title.set('Tulips');
    await fixture.whenStable();
    await settle();

    expect(animator.cancelled).toBe(1);
    expect(copy()).toBeNull();
    expect(h1.style.color).toBe('');
    expect(realText()).toBe('Tulips');
  });

  // Protects against: an invisible title when the animation library fails to load (offline, a failed download).
  it('shows the real title again when the animation fails', async () => {
    stubReducedMotion(false);
    TestBed.overrideProvider(TITLE_ANIMATOR, {
      useValue: () => ({ finished: Promise.reject(new Error('chunk failed')), cancel: () => {} }),
    });
    const { h1, copy } = await render();

    expect(copy()).toBeNull();
    expect(h1.style.color).toBe('');
  });

  // Protects against: an animation left running on a page that is gone.
  it('cancels on destroy', async () => {
    stubReducedMotion(false);
    const { fixture } = await render();

    fixture.destroy();

    expect(animator.cancelled).toBe(1);
  });
});
