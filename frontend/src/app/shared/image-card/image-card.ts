import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icon } from '../icons/icon';
import { Reveal } from '../reveal';

/**
 * Image-first navigation card (UX §17, §22–23). Without an image it shows an outlined leaf on the warm surface.
 * The image is decorative (empty alt) because the visible name already labels the link. Cards fade in as they
 * come into view (Reveal), and on desktop the image zooms slightly on hover.
 * Deferred: bookmark affordance, hover information, masonry layout.
 */
@Component({
  selector: 'app-image-card',
  imports: [RouterLink, Icon],
  hostDirectives: [Reveal],
  template: `
    <a class="card" [routerLink]="link()">
      <span class="image">
        @if (imageUrl(); as url) {
          <img [src]="url" alt="" loading="lazy" />
        } @else {
          <app-icon class="placeholder" name="leaf" />
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
