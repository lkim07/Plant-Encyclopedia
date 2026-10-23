import { HttpClient } from '@angular/common/http';
import { Component, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of } from 'rxjs';
import { ApiResponse } from '../../core/api/api-response.model';

/** GET /api/auth/providers item. */
interface Provider {
  readonly id: 'google' | 'github';
  readonly name: string;
}

/**
 * "Continue with Google / GitHub" (D-070, Phase 6a-2), only for providers the API has keys for.
 * Each is a full-page link: the API sends the browser to the provider and back. The logos keep their brand colours,
 * as the providers' brand rules require; they are the one exception to the brown outlined icons.
 */
@Component({
  selector: 'app-provider-buttons',
  template: `
    @if (providers().length > 0) {
      <div class="providers">
        @for (provider of providers(); track provider.id) {
          <a class="provider" [href]="startUrl(provider.id)">
            @switch (provider.id) {
              @case ('google') {
                <svg viewBox="0 0 48 48" aria-hidden="true" focusable="false">
                  <path
                    fill="#EA4335"
                    d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"
                  />
                  <path
                    fill="#4285F4"
                    d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"
                  />
                  <path
                    fill="#FBBC05"
                    d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z"
                  />
                  <path
                    fill="#34A853"
                    d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"
                  />
                </svg>
              }
              @case ('github') {
                <svg viewBox="0 0 16 16" aria-hidden="true" focusable="false">
                  <path
                    fill="#181717"
                    d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"
                  />
                </svg>
              }
            }
            <span>Continue with {{ provider.name }}</span>
          </a>
        }
      </div>
      <p class="divider"><span>or</span></p>
    }
  `,
  styles: `
    .providers {
      display: flex;
      flex-direction: column;
      gap: var(--space-2);
    }

    .provider {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: var(--space-3);
      min-height: var(--touch-target);
      padding: 0 var(--space-4);
      border: 1px solid var(--color-surface-strong);
      border-radius: var(--radius-sm);
      background: var(--color-bg);
      font-weight: 600;
      text-decoration: none;
      transition: background-color var(--transition-fast);
    }

    .provider:hover {
      background: var(--color-surface-strong);
    }

    .provider svg {
      width: 1.25rem;
      height: 1.25rem;
      flex: none;
    }

    .divider {
      display: flex;
      align-items: center;
      gap: var(--space-3);
      margin: 0;
      color: var(--color-text-muted);
      font-size: 0.875rem;
    }

    .divider::before,
    .divider::after {
      content: '';
      flex: 1;
      height: 1px;
      background: var(--color-surface-strong);
    }
  `,
})
export class ProviderButtons {
  readonly returnUrl = input('/explore');

  protected readonly providers = toSignal(
    inject(HttpClient)
      .get<ApiResponse<readonly Provider[]>>('/api/auth/providers')
      .pipe(
        map((response) => response.data),
        catchError(() => of([] as readonly Provider[])),
      ),
    { initialValue: [] as readonly Provider[] },
  );

  protected startUrl(id: string): string {
    return `/api/auth/external/${id}?returnUrl=${encodeURIComponent(this.returnUrl())}`;
  }
}
