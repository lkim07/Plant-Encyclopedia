import { Component, input, signal } from '@angular/core';

/**
 * One Quick Care card (UX §29). When there is an explanation, the whole card is a button
 * that expands to show it; otherwise it is a plain, non-interactive card.
 */
@Component({
  selector: 'app-care-card',
  template: `
    @if (description(); as details) {
      <button
        type="button"
        class="card"
        [attr.aria-expanded]="expanded()"
        (click)="expanded.set(!expanded())"
      >
        <span class="title"><span aria-hidden="true">{{ icon() }}</span> {{ label() }}</span>
        <span class="value">{{ value() }}</span>
        @if (expanded()) {
          <span class="details">{{ details }}</span>
        }
        <span class="arrow" aria-hidden="true">{{ expanded() ? '↑' : '↓' }}</span>
      </button>
    } @else {
      <div class="card">
        <span class="title"><span aria-hidden="true">{{ icon() }}</span> {{ label() }}</span>
        <span class="value">{{ value() }}</span>
      </div>
    }
  `,
  styleUrl: './care-card.css',
})
export class CareCard {
  readonly icon = input.required<string>();
  readonly label = input.required<string>();
  readonly value = input.required<string>();
  readonly description = input<string | null>(null);

  protected readonly expanded = signal(false);
}
