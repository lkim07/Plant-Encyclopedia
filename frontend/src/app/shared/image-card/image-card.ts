import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Image-first navigation card (UX §17, §22–23). Shows a warm placeholder until images exist.
 * The image is decorative (empty alt) because the visible name already labels the link.
 * Deferred: bookmark affordance, desktop hover treatment, masonry layout.
 */
@Component({
  selector: 'app-image-card',
  imports: [RouterLink],
  template: `
    <a class="card" [routerLink]="link()">
      <span class="image">
        @if (imageUrl(); as url) {
          <img [src]="url" alt="" loading="lazy" />
        }
      </span>
      <span class="name">{{ name() }}</span>
    </a>
  `,
  styleUrl: './image-card.css',
})
export class ImageCard {
  readonly name = input.required<string>();
  readonly link = input.required<string>();
  readonly imageUrl = input<string | null>(null);
}
