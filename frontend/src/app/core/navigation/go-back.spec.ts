import { Location } from '@angular/common';
import { Router } from '@angular/router';
import { goBack } from './go-back';

describe('goBack', () => {
  function setup(state: unknown) {
    const location = { getState: vi.fn(() => state), back: vi.fn() };
    const router = { navigateByUrl: vi.fn(() => Promise.resolve(true)) };
    goBack(location as unknown as Location, router as unknown as Router, '/categories/flowers');
    return { location, router };
  }

  // Protects against: Back ignoring the previous in-app screen (PRD §32).
  it('goes back through history when the page was reached inside the app', () => {
    const { location, router } = setup({ navigationId: 3 });

    expect(location.back).toHaveBeenCalledOnce();
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  // Protects against: Back leaving the app, or the fallback adding an entry that Back returns through.
  it('replaces the entry with the fallback when the page was opened directly', () => {
    const { location, router } = setup({ navigationId: 1 });

    expect(location.back).not.toHaveBeenCalled();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/categories/flowers', {
      replaceUrl: true,
      state: { backFallback: true },
    });
  });

  // Protects against: the Roses → Flowers → Roses loop after a direct visit.
  it('uses the fallback again on a page reached through a fallback', () => {
    const { location, router } = setup({ navigationId: 2, backFallback: true });

    expect(location.back).not.toHaveBeenCalled();
    expect(router.navigateByUrl).toHaveBeenCalledOnce();
  });

  it('uses the fallback when there is no history state', () => {
    const { location, router } = setup(null);

    expect(location.back).not.toHaveBeenCalled();
    expect(router.navigateByUrl).toHaveBeenCalledOnce();
  });
});
