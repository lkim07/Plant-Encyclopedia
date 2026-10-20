import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ImageCard } from './image-card';

@Component({
  imports: [ImageCard],
  template: `<ul>
    <li><app-image-card name="Dog rose" link="/plants/1" [imageUrl]="imageUrl()" /></li>
  </ul>`,
})
class Host {
  readonly imageUrl = signal<string | null>(null);
}

/** Lets tests decide when the card comes into view. */
class FakeIntersectionObserver {
  static last?: FakeIntersectionObserver;
  constructor(private readonly callback: IntersectionObserverCallback) {
    FakeIntersectionObserver.last = this;
  }
  observe(): void {}
  disconnect(): void {}
  enterView(): void {
    this.callback(
      [{ isIntersecting: true } as IntersectionObserverEntry],
      this as unknown as IntersectionObserver,
    );
  }
}

function stubMotionPreference(reduce: boolean): void {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: query.includes('prefers-reduced-motion: reduce') ? reduce : false,
  }));
}

describe('ImageCard', () => {
  beforeEach(() => {
    FakeIntersectionObserver.last = undefined;
    vi.stubGlobal('IntersectionObserver', FakeIntersectionObserver);
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
  });

  afterEach(() => vi.unstubAllGlobals());

  async function render(imageUrl: string | null = null): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.imageUrl.set(imageUrl);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  // Protects against: an empty box for plants without images.
  it('shows an outlined leaf when there is no image, and the image when there is one', async () => {
    stubMotionPreference(false);
    expect((await render()).querySelector('app-icon')?.getAttribute('data-icon')).toBe('leaf');

    const withImage = await render('/media/a-medium.webp');
    expect(withImage.querySelector('img')?.getAttribute('src')).toBe('/media/a-medium.webp');
    expect(withImage.querySelector('app-icon')).toBeNull();
  });

  // Protects against: cards that never appear, or that stay hidden until they are scrolled to.
  it('fades in when it comes into view', async () => {
    stubMotionPreference(false);
    const card = (await render()).querySelector('app-image-card')!;

    expect(card.classList.contains('reveal')).toBe(true);
    expect(card.classList.contains('revealed')).toBe(false);

    FakeIntersectionObserver.last!.enterView();
    expect(card.classList.contains('revealed')).toBe(true);
  });

  // Protects against: decorative motion for visitors who asked for less (UX §51).
  it('is never hidden or animated with reduced motion', async () => {
    stubMotionPreference(true);
    const card = (await render()).querySelector('app-image-card')!;

    expect(card.classList.contains('reveal')).toBe(false);
    expect(FakeIntersectionObserver.last).toBeUndefined();
  });
});
