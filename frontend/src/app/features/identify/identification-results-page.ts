import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { IdentifySession } from '../../core/identification/identify-session';
import { Icon } from '../../shared/icons/icon';

/**
 * Identification Results (PRD §9, UX §10–11, D-012–D-014): the #1 candidate large, then #2, #3 and More. Every
 * candidate is an encyclopedia plant (the API only returns validated matches) and opens Plant Detail; Back from there
 * returns here. No confidence values are shown (D-013). More opens the #1 candidate's plant group until the
 * contextual More page exists (8c). Opened without a current identification (e.g. after a reload), it goes back to
 * Identify.
 */
@Component({
  selector: 'app-identification-results-page',
  imports: [RouterLink, Icon],
  templateUrl: './identification-results-page.html',
  styleUrl: './identification-results-page.css',
})
export class IdentificationResultsPage {
  protected readonly session = inject(IdentifySession);

  protected readonly candidates = computed(() => this.session.candidates() ?? []);
  protected readonly top = computed(() => this.candidates()[0] ?? null);
  protected readonly others = computed(() => this.candidates().slice(1, 3));

  constructor() {
    if (this.session.candidates() === null) {
      void inject(Router).navigateByUrl('/identify', { replaceUrl: true });
    }
  }
}
