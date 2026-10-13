import { Location } from '@angular/common';
import { Router } from '@angular/router';

interface BackState {
  /** Angular's per-entry navigation counter; 1 means the app was loaded on this entry. */
  readonly navigationId?: number;
  /** Set on an entry reached through a Back fallback; its own Back must not return to the page it replaced. */
  readonly backFallback?: boolean;
}

/**
 * Back follows in-app history (PRD §32). A page opened directly has no in-app history,
 * so Back goes to `fallbackUrl` instead of leaving the app.
 *
 * The fallback replaces the current history entry rather than adding one. Otherwise Back on
 * the fallback page would return to the page just left (Roses → Flowers → Roses). Back from a
 * fallback page uses its own fallback, so repeated Back walks up the hierarchy.
 */
export function goBack(location: Location, router: Router, fallbackUrl: string): void {
  const state = location.getState() as BackState | null;
  const hasInAppHistory = (state?.navigationId ?? 0) > 1 && !state?.backFallback;
  if (hasInAppHistory) {
    location.back();
  } else {
    void router.navigateByUrl(fallbackUrl, { replaceUrl: true, state: { backFallback: true } });
  }
}
