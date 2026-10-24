import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BookmarkButton } from '../bookmark/bookmark-button';
import { Icon } from '../icons/icon';
import { Reveal } from '../reveal';

/**
 * Image-first navigation card (UX §17, §22–23). Without an image it shows an outlined leaf on the warm surface.
 * The image is decorative (empty alt) because the visible name already labels the link. Cards fade in as they
 * come into view (Reveal), and on desktop the image zooms slightly on hover. Plant cards (with `plantId`) carry a
 * bookmark over the image; it sits beside the link, not inside it, so selecting it never opens the plant.
 * Deferred: hover information, masonry layout.
 */
@Component({
  selector: 'app-image-card',
  imports: [RouterLink, Icon, BookmarkButton],
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
    @if (plantId(); as id) {
      <app-bookmark-button
        class="bookmark"
        [plantId]="id"
        [plantName]="name()"
        [undo]="bookmarkUndo()"
      />
    }
  `,
  styleUrl: './image-card.css',
})
export class ImageCard {
  readonly name = input.required<string>();
  readonly link = input.required<string>();
  readonly imageUrl = input<string | null>(null);
  /** A plant card: shows the bookmark for this plant. */
  readonly plantId = input<string | null>(null);
  /** Removing the bookmark offers Undo (Favorites page). */
  readonly bookmarkUndo = input(false);
}
