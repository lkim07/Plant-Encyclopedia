import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Auth } from '../../core/auth/auth';
import { FormSubmission } from './account-form';

/** Ask for a reset link. The answer is the same whether or not the address has an account. */
@Component({
  selector: 'app-forgot-password-page',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="account" aria-labelledby="forgot-heading">
      <h1 id="forgot-heading">Forgot your password?</h1>
      @if (sent()) {
        <p class="lead" role="status">
          If an account uses that address, we've sent a link to choose a new password. It expires in
          3 hours.
        </p>
        <a routerLink="/login">Back to sign in</a>
      } @else {
        <p class="lead">
          Enter your email address and we'll send you a link to choose a new password.
        </p>
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div class="field">
            <label for="forgot-email">Email</label>
            <input id="forgot-email" type="email" formControlName="email" autocomplete="email" />
          </div>
          @if (submission.error(); as error) {
            <p class="form-error" role="alert">{{ error.message }}</p>
          }
          <button type="submit" class="action" [disabled]="submission.busy()">
            {{ submission.busy() ? 'Sending…' : 'Send link' }}
          </button>
        </form>
        <a routerLink="/login">Back to sign in</a>
      }
    </section>
  `,
  styleUrl: './account-page.css',
})
export class ForgotPasswordPage {
  private readonly auth = inject(Auth);

  protected readonly submission = new FormSubmission();
  protected readonly sent = signal(false);
  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.submission.error.set({
        code: 'VALIDATION_ERROR',
        message: 'Enter a valid email address.',
        fields: {},
      });
      return;
    }
    this.submission.run(this.auth.forgotPassword(this.form.getRawValue().email.trim()), () =>
      this.sent.set(true),
    );
  }
}

/** Choose a new password from the emailed link (/reset-password?email=…&token=…). */
@Component({
  selector: 'app-reset-password-page',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="account" aria-labelledby="reset-heading">
      <h1 id="reset-heading">Choose a new password</h1>
      @if (!email || !token) {
        <p class="lead" role="alert">This reset link is incomplete. Ask for a new one.</p>
        <a routerLink="/forgot-password">Send a new link</a>
      } @else if (done()) {
        <p class="lead" role="status">Your password has been changed.</p>
        <a routerLink="/login">Sign in</a>
      } @else {
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div class="field">
            <label for="reset-password">New password</label>
            <input
              id="reset-password"
              type="password"
              formControlName="password"
              autocomplete="new-password"
              maxlength="128"
              aria-describedby="reset-password-hint"
            />
            <span id="reset-password-hint" class="hint">At least 8 characters.</span>
          </div>
          @if (submission.error(); as error) {
            <p class="form-error" role="alert">
              {{ submission.field('password') ?? error.message }}
            </p>
            @if (error.code === 'INVALID_RESET_LINK') {
              <a routerLink="/forgot-password">Send a new link</a>
            }
          }
          <button type="submit" class="action" [disabled]="submission.busy()">
            {{ submission.busy() ? 'Saving…' : 'Save password' }}
          </button>
        </form>
      }
    </section>
  `,
  styleUrl: './account-page.css',
})
export class ResetPasswordPage {
  private readonly auth = inject(Auth);
  private readonly query = inject(ActivatedRoute).snapshot.queryParamMap;

  protected readonly email = this.query.get('email');
  protected readonly token = this.query.get('token');
  protected readonly submission = new FormSubmission();
  protected readonly done = signal(false);
  protected readonly form = inject(NonNullableFormBuilder).group({
    password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]],
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.submission.error.set({
        code: 'VALIDATION_ERROR',
        message: 'Use at least 8 characters.',
        fields: {},
      });
      return;
    }
    this.submission.run(
      this.auth.resetPassword(this.email!, this.token!, this.form.getRawValue().password),
      () => this.done.set(true),
    );
  }
}

/** Where the confirmation link lands (/email-confirmed?status=ok|invalid). */
@Component({
  selector: 'app-email-confirmed-page',
  imports: [RouterLink],
  template: `
    <section class="account" aria-labelledby="confirmed-heading">
      @if (ok) {
        <h1 id="confirmed-heading">Email confirmed</h1>
        <p class="lead">Thank you. You can now save plants to your Favorites.</p>
        <a routerLink="/explore">Explore plants</a>
      } @else {
        <h1 id="confirmed-heading">This link didn't work</h1>
        <p class="lead">
          It may have expired or been used already. Sign in to send a new confirmation email.
        </p>
        <a routerLink="/login">Sign in</a>
      }
    </section>
  `,
  styleUrl: './account-page.css',
})
export class EmailConfirmedPage {
  protected readonly ok = inject(ActivatedRoute).snapshot.queryParamMap.get('status') === 'ok';

  constructor() {
    // The session's user now has a confirmed email; ask again so the app knows.
    inject(Auth).refresh().subscribe();
  }
}
