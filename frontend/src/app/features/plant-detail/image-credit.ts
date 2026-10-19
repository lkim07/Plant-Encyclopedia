import { Component, computed, input } from '@angular/core';
import { ImageCredit } from '../../core/api/plant-detail.model';

/**
 * Credit line for an image, as its licence requires: "Photo: Douglas Goldman · CC BY-SA · iNaturalist".
 * The licence and source link out when their URLs are known; external links open in a new tab.
 */
@Component({
  selector: 'app-image-credit',
  template: `
    @let c = credit();
    {{ label() }}:
    @if (c.author) {
      {{ c.author }} ·
    }
    @if (c.licenseUrl) {
      <a [href]="c.licenseUrl" target="_blank" rel="noopener noreferrer">{{ c.license }}</a>
    } @else {
      {{ c.license }}
    }
    ·
    @if (c.sourceUrl) {
      <a [href]="c.sourceUrl" target="_blank" rel="noopener noreferrer">{{ c.source }}</a>
    } @else {
      {{ c.source }}
    }
  `,
})
export class ImageCreditLine {
  readonly credit = input.required<ImageCredit>();
  readonly kind = input.required<'photo' | 'illustration'>();

  protected readonly label = computed(() => (this.kind() === 'photo' ? 'Photo' : 'Illustration'));
}
