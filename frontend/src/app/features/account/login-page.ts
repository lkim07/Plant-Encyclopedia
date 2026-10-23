import { Component, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Auth } from '../../core/auth/auth';
import { FormSubmission, safeReturnUrl } from './account-form';
import { ProviderButtons } from './provider-buttons';

/** Why a provider sign-in came back to this page (/login?error=…). */
const PROVIDER_ERRORS: Readonly<Record<string, string>> = {
  'email-in-use':
    'An account already uses this email address. Sign in with your email and password instead.',
  'email-required':
    "That account didn't share an email address with us. Sign in another way, or create an account with your email.",
  locked: 'This account is locked after too many failed sign-ins. Try again in 15 minutes.',
  'external-failed': "Signing in with that provider didn't work. Please try again.",
};

/**
 * Sign in with Google or GitHub, or with email and password (D-070). After signing in, returns to
 * `returnUrl` (paths in this app only).
 */
@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink, ProviderButtons],
  template: `
    <section class="account" aria-labelledby="login-heading">
      <h1 id="login-heading">Sign in</h1>
      <p class="lead">Sign in to save plants to your Favorites.</p>

      @if (providerError) {
        <p class="form-error" role="alert">{{ providerError }}</p>
      }

      <app-provider-buttons [returnUrl]="returnUrl" />

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <div class="field">
          <label for="login-email">Email</label>
          <input id="login-email" type="email" formControlName="email" autocomplete="email" />
        </div>
        <div class="field">
          <label for="login-password">Password</label>
          <input
            id="login-password"
            type="password"
            formControlName="password"
            autocomplete="current-password"
          />
        </div>

        @if (submission.error(); as error) {
          <p class="form-error" role="alert">{{ error.message }}</p>
        }

        <button type="submit" class="action" [disabled]="submission.busy()">
          {{ submission.busy() ? 'Signing in…' : 'Sign in' }}
        </button>
      </form>

      <div class="links">
        <a routerLink="/forgot-password">Forgot password?</a>
        <a routerLink="/signup" [queryParams]="{ returnUrl: returnUrl }">Create an account</a>
      </div>
    </section>
  `,
  styleUrl: './account-page.css',
})
export class LoginPage {
  private readonly auth = inject(Auth);
  private readonly router = inject(Router);

  private readonly query = inject(ActivatedRoute).snapshot.queryParamMap;

  protected readonly returnUrl = safeReturnUrl(this.query.get('returnUrl'));
  protected readonly providerError = PROVIDER_ERRORS[this.query.get('error') ?? ''] ?? null;
  protected readonly submission = new FormSubmission();
  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.submission.error.set({
        code: 'VALIDATION_ERROR',
        message: 'Enter your email address and password.',
        fields: {},
      });
      return;
    }
    const { email, password } = this.form.getRawValue();
    this.submission.run(
      this.auth.login(email.trim(), password),
      () => void this.router.navigateByUrl(this.returnUrl),
    );
  }
}
