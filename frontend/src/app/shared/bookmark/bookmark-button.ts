import { Component, computed, inject, input } from '@angular/core';
import { Favorites } from '../../core/favorites/favorites';
import { Icon } from '../icons/icon';

/**
 * The bookmark toggle (PRD §18, UX §38): outlined when the plant is not saved, filled when it is. Selecting it
 * saves or removes the plant; signed out, it opens the "Save this plant" prompt. It reports its state with
 * aria-pressed under one fixed label. It never follows the card link it sits on.
 */
@Component({
  selector: 'app-bookmark-button',
  imports: [Icon],
  template: `
    <button
      type="button"
      class="icon-button bookmark"
      [attr.aria-pressed]="saved()"
      [attr.aria-label]="'Save ' + plantName() + ' to Favorites'"
      (click)="toggle($event)"
    >
      <app-icon name="bookmark" [filled]="saved()" />
    </button>
  `,
  styles: `
    :host {
      display: inline-flex;
    }

    .bookmark {
      border-radius: 999px;
      background: color-mix(in srgb, var(--color-bg) 90%, transparent);
      box-shadow: var(--shadow-card);
      transition: transform var(--transition-fast);
    }

    .bookmark:active {
      transform: scale(0.92);
    }
  `,
})
export class BookmarkButton {
  private readonly favorites = inject(Favorites);

  readonly plantId = input.required<string>();
  readonly plantName = input.required<string>();
  /** On the Favorites page: removing offers Undo. */
  readonly undo = input(false);

  protected readonly saved = computed(() => this.favorites.isSaved(this.plantId()));

  protected toggle(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.favorites.toggle({ id: this.plantId(), name: this.plantName() }, { undo: this.undo() });
  }
}
