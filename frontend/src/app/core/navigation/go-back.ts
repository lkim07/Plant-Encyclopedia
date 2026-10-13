import { Location } from '@angular/common';
import { Router } from '@angular/router';

/**
 * Back follows in-app history (PRD §32). A page opened directly has no in-app history,
 * so Back goes to `fallbackUrl` instead of leaving the app.
 */
export function goBack(location: Location, router: Router, fallbackUrl: string): void {
  const navigationId = (location.getState() as { navigationId?: number } | null)?.navigationId;
  if (navigationId !== undefined && navigationId > 1) {
    location.back();
  } else {
    void router.navigateByUrl(fallbackUrl);
  }
}
