import { signal } from '@angular/core';
import { Observable } from 'rxjs';
import { AccountError } from '../../core/auth/auth';

/** Where to go after signing in: only paths inside this app ("/plants/…"), never another site. */
export function safeReturnUrl(value: string | null | undefined): string {
  return value && value.startsWith('/') && !value.startsWith('//') && !value.startsWith('/\\')
    ? value
    : '/explore';
}

/** Submit state shared by the account forms: busy while waiting, then the API's error (message and field messages). */
export class FormSubmission {
  readonly busy = signal(false);
  readonly error = signal<AccountError | null>(null);

  run<T>(request: Observable<T>, done: (value: T) => void): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: (value) => {
        this.busy.set(false);
        done(value);
      },
      error: (error: AccountError) => {
        this.busy.set(false);
        this.error.set(error);
      },
    });
  }

  /** The server's message for one field, if it sent one. */
  field(name: string): string | null {
    return this.error()?.fields[name] ?? null;
  }
}
