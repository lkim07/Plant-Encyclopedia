import { Component, effect, ElementRef, inject, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { Favorites } from '../../core/favorites/favorites';

/**
 * "Save this plant" (UX §39, PRD §19): shown when a signed-out user selects a bookmark. A bottom sheet on phones,
 * a centred dialog on larger screens. It is a native modal <dialog> (focus stays inside, Esc closes).
 * "Sign in" remembers the plant and returns here after signing in, where the plant is saved.
 */
@Component({
  selector: 'app-save-prompt',
  template: `
    <dialog
      #dialog
      class="sheet"
      aria-labelledby="save-prompt-heading"
      aria-describedby="save-prompt-text"
      (close)="favorites.prompt.set(null)"
      (click)="onDialogClick($event)"
    >
      <div class="panel">
        <h2 id="save-prompt-heading">Save this plant</h2>
        <p id="save-prompt-text">Sign in to save plants to your Favorites.</p>
        <div class="actions">
          <button type="button" class="sign-in" (click)="signIn()">Sign in</button>
          <button type="button" class="cancel" (click)="cancel()">Cancel</button>
        </div>
      </div>
    </dialog>
  `,
  styles: `
    .sheet {
      width: 100%;
      max-width: none;
      margin: auto 0 0;
      padding: 0;
      border: none;
      border-radius: var(--radius-md) var(--radius-md) 0 0;
      background: var(--color-surface);
      color: var(--color-text);
    }

    .sheet::backdrop {
      background: var(--color-backdrop);
    }

    .panel {
      display: flex;
      flex-direction: column;
      gap: var(--space-3);
      padding: var(--space-6) var(--space-6) calc(var(--space-6) + env(safe-area-inset-bottom, 0px));
    }

    h2,
    p {
      margin: 0;
    }

    p {
      color: var(--color-text-muted);
    }

    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-2);
      margin-top: var(--space-2);
    }

    .actions button {
      flex: 1;
      min-height: var(--touch-target);
      padding: 0 var(--space-6);
      border-radius: var(--radius-sm);
      font-weight: 600;
      cursor: pointer;
    }

    .sign-in {
      border: none;
      background: var(--color-text);
      color: var(--color-bg);
    }

    .cancel {
      border: 1px solid var(--color-text);
      background: transparent;
    }

    @media (prefers-reduced-motion: no-preference) {
      .sheet[open] {
        animation: sheet-up 220ms var(--ease-out-soft);
      }
    }

    @keyframes sheet-up {
      from {
        transform: translateY(100%);
      }
    }

    /* Larger screens: a centred dialog. */
    @media (min-width: 48rem) {
      .sheet {
        width: min(26rem, 90vw);
        margin: auto;
        border-radius: var(--radius-md);
      }

      .panel {
        padding: var(--space-6);
      }

      .actions button {
        flex: none;
      }

      @media (prefers-reduced-motion: no-preference) {
        .sheet[open] {
          animation: fade-in 150ms ease-out;
        }
      }
    }

    @keyframes fade-in {
      from {
        opacity: 0;
      }
    }
  `,
})
export class SavePrompt {
  protected readonly favorites = inject(Favorites);
  private readonly router = inject(Router);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    effect(() => {
      const plant = this.favorites.prompt();
      const dialog = this.dialog().nativeElement;
      if (plant && !dialog.open) {
        dialog.showModal();
      } else if (!plant && dialog.open) {
        dialog.close();
      }
    });
  }

  protected signIn(): void {
    const plant = this.favorites.prompt();
    if (plant) {
      this.favorites.rememberPending(plant.id);
    }
    const returnUrl = this.router.url;
    this.favorites.prompt.set(null);
    void this.router.navigate(['/login'], { queryParams: { returnUrl } });
  }

  protected cancel(): void {
    this.favorites.prompt.set(null);
  }

  /** A click on the <dialog> itself landed on the backdrop, outside the panel. */
  protected onDialogClick(event: MouseEvent): void {
    if (event.target === this.dialog().nativeElement) {
      this.cancel();
    }
  }
}
