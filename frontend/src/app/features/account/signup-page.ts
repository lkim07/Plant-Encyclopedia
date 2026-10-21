import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Auth } from '../../core/auth/auth';
import { FormSubmission, safeReturnUrl } from './account-form';

/**
 * Create an account with email and password (D-070). The new account is signed in straight away; confirming the
 * email address is needed only to save Favorites, so the page then asks the user to check their email.
 */
@Component({
  selector: 'app-signup-page',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="account" aria-labelledby="signup-heading">
      @if (createdFor(); as email) {
        <h1 id="signup-heading">Check your email</h1>
        <p class="lead">
          We sent a link to <strong>{{ email }}</strong
          >. Confirm your address to save plants to your Favorites. You can keep exploring in the
          meantime.
        </p>
        <a class="action as-link" [routerLink]="returnUrl">Continue</a>
      } @else {
        <h1 id="signup-heading">Create an account</h1>

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div class="field">
            <label for="signup-name">Name</label>
            <input
              id="signup-name"
              type="text"
              formControlName="displayName"
              autocomplete="name"
              maxlength="50"
              [attr.aria-invalid]="!!submission.field('displayName')"
              aria-describedby="signup-name-error"
            />
            @if (submission.field('displayName'); as message) {
              <p id="signup-name-error" class="field-error">{{ message }}</p>
            }
          </div>
          <div class="field">
            <label for="signup-email">Email</label>
            <input
              id="signup-email"
              type="email"
              formControlName="email"
              autocomplete="email"
              [attr.aria-invalid]="!!submission.field('email')"
              aria-describedby="signup-email-error"
            />
            @if (submission.field('email'); as message) {
              <p id="signup-email-error" class="field-error">{{ message }}</p>
            }
          </div>
          <div class="field">
            <label for="signup-password">Password</label>
            <input
              id="signup-password"
              type="password"
              formControlName="password"
              autocomplete="new-password"
              maxlength="128"
              [attr.aria-invalid]="!!submission.field('password')"
              aria-describedby="signup-password-hint signup-password-error"
            />
            <span id="signup-password-hint" class="hint"
              >At least 8 characters. A short phrase works well.</span
            >
            @if (submission.field('password'); as message) {
              <p id="signup-password-error" class="field-error">{{ message }}</p>
            }
          </div>

          @if (submission.error(); as error) {
            <p class="form-error" role="alert">{{ error.message }}</p>
          }

          <button type="submit" class="action" [disabled]="submission.busy()">
            {{ submission.busy() ? 'Creating account…' : 'Create account' }}
          </button>
        </form>

        <div class="links">
          <span class="muted">Already have an account?</span>
          <a routerLink="/login" [queryParams]="{ returnUrl: returnUrl }">Sign in</a>
        </div>
      }
    </section>
  `,
  styleUrl: './account-page.css',
  styles: `
    .as-link {
      display: inline-grid;
      place-items: center;
      align-self: flex-start;
      text-decoration: none;
    }
  `,
})
export class SignupPage {
  private readonly auth = inject(Auth);

  protected readonly returnUrl = safeReturnUrl(
    inject(ActivatedRoute).snapshot.queryParamMap.get('returnUrl'),
  );
  protected readonly submission = new FormSubmission();
  protected readonly createdFor = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    displayName: ['', [Validators.required, Validators.maxLength(50)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]],
  });

  protected submit(): void {
    const { displayName, email, password } = this.form.getRawValue();
    const fields: Record<string, string> = {};
    if (!displayName.trim()) fields['displayName'] = 'Enter your name.';
    if (this.form.controls.email.invalid) fields['email'] = 'Enter a valid email address.';
    if (password.length < 8) fields['password'] = 'Use at least 8 characters.';
    if (Object.keys(fields).length > 0) {
      this.submission.error.set({
        code: 'VALIDATION_ERROR',
        message: 'Check the highlighted fields.',
        fields,
      });
      return;
    }

    this.submission.run(this.auth.register(email.trim(), password, displayName.trim()), (user) =>
      this.createdFor.set(user.email),
    );
  }
}
