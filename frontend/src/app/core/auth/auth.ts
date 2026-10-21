import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, map, Observable, of, tap } from 'rxjs';
import { ApiErrorResponse, ApiResponse } from '../api/api-response.model';

/** GET /api/me data (API_SPEC §6). */
export interface User {
  readonly id: string;
  readonly displayName: string;
  readonly email: string | null;
  readonly emailConfirmed: boolean;
}

/** A failed account request: the API's message, plus field messages for forms. */
export interface AccountError {
  readonly code: string;
  readonly message: string;
  readonly fields: Readonly<Record<string, string>>;
}

/**
 * The signed-in user and the account endpoints (D-070). The session is an HttpOnly cookie the app never sees;
 * requests that change data carry the CSRF token, which Angular's HttpClient copies from the XSRF-TOKEN cookie
 * into the X-XSRF-TOKEN header. `user` is undefined until the first check finishes, then a User or null.
 */
@Injectable({ providedIn: 'root' })
export class Auth {
  private readonly http = inject(HttpClient);
  private readonly current = signal<User | null | undefined>(undefined);

  readonly user = this.current.asReadonly();
  readonly signedIn = computed(() => !!this.current());

  /** Asks who is signed in (and receives a fresh CSRF token). A failure counts as signed out. */
  refresh(): Observable<User | null> {
    return this.http.get<ApiResponse<User>>('/api/me').pipe(
      map((response) => response.data),
      catchError(() => of(null)),
      tap((user) => this.current.set(user)),
    );
  }

  register(email: string, password: string, displayName: string): Observable<User> {
    return this.signInWith(
      this.http.post<ApiResponse<User>>('/api/auth/register', { email, password, displayName }),
    );
  }

  login(email: string, password: string): Observable<User> {
    return this.signInWith(
      this.http.post<ApiResponse<User>>('/api/auth/login', { email, password }),
    );
  }

  logout(): Observable<void> {
    return this.http.post<void>('/api/auth/logout', {}).pipe(
      tap(() => this.current.set(null)),
      catchError((error) => {
        throw toAccountError(error);
      }),
    );
  }

  forgotPassword(email: string): Observable<void> {
    return this.http.post('/api/auth/forgot-password', { email }).pipe(
      map(() => undefined),
      catchError((error) => {
        throw toAccountError(error);
      }),
    );
  }

  resetPassword(email: string, token: string, password: string): Observable<void> {
    return this.http.post('/api/auth/reset-password', { email, token, password }).pipe(
      map(() => undefined),
      catchError((error) => {
        throw toAccountError(error);
      }),
    );
  }

  resendConfirmation(): Observable<void> {
    return this.http.post('/api/auth/resend-confirmation', {}).pipe(
      map(() => undefined),
      catchError((error) => {
        throw toAccountError(error);
      }),
    );
  }

  private signInWith(request: Observable<ApiResponse<User>>): Observable<User> {
    return request.pipe(
      map((response) => response.data),
      tap((user) => this.current.set(user)),
      catchError((error) => {
        throw toAccountError(error);
      }),
    );
  }
}

/** The API's error body as an AccountError; network and unexpected errors get a general message. */
export function toAccountError(error: unknown): AccountError {
  const body = error instanceof HttpErrorResponse ? (error.error as ApiErrorResponse | null) : null;
  if (body?.error) {
    return { code: body.error.code, message: body.error.message, fields: body.error.details ?? {} };
  }
  if (error instanceof HttpErrorResponse && error.status === 429) {
    return {
      code: 'RATE_LIMITED',
      message: 'Too many attempts. Please wait a moment and try again.',
      fields: {},
    };
  }
  return {
    code: 'UNEXPECTED',
    message: 'Something went wrong. Please check your connection and try again.',
    fields: {},
  };
}
