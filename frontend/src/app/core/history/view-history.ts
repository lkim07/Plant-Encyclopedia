import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

/**
 * Records that a signed-in user opened a plant (API_SPEC §29), as a recommendation signal (PRD §17). Nothing is
 * shown to the user, so a failed request is ignored. Visitors who are not signed in have no view history.
 */
@Injectable({ providedIn: 'root' })
export class ViewHistory {
  private readonly http = inject(HttpClient);

  record(plantId: string): void {
    this.http.post('/api/history/views', { plantId }).subscribe({ error: () => undefined });
  }
}
