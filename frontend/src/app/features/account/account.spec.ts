import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../../app.routes';
import { Auth, User } from '../../core/auth/auth';
import { safeReturnUrl } from './account-form';

const USER: User = {
  id: 'u1',
  displayName: 'Rose Lover',
  email: 'rose@example.org',
  emailConfirmed: false,
};

describe('Accounts', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function open(url: string): Promise<{ harness: RouterTestingHarness; page: HTMLElement }> {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    await harness.fixture.whenStable();
    return { harness, page: harness.routeNativeElement as HTMLElement };
  }

  async function settle(harness: RouterTestingHarness): Promise<HTMLElement> {
    await harness.fixture.whenStable();
    return harness.routeNativeElement as HTMLElement;
  }

  function type(page: HTMLElement, selector: string, value: string): void {
    const input = page.querySelector<HTMLInputElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function submit(page: HTMLElement): void {
    page.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
  }

  // Protects against: an open redirect after sign-in (returnUrl pointing at another site).
  it('only returns to paths inside the app after signing in', () => {
    expect(safeReturnUrl('/plants/1')).toBe('/plants/1');
    expect(safeReturnUrl('//evil.example')).toBe('/explore');
    expect(safeReturnUrl('/\\evil.example')).toBe('/explore');
    expect(safeReturnUrl('https://evil.example')).toBe('/explore');
    expect(safeReturnUrl(null)).toBe('/explore');
  });

  // Protects against: the app thinking someone is signed in after the session ended, or the reverse.
  it('tracks the signed-in user', () => {
    const auth = TestBed.inject(Auth);
    expect(auth.user()).toBeUndefined();

    auth.refresh().subscribe();
    http
      .expectOne('/api/me')
      .flush(
        { error: { code: 'UNAUTHENTICATED', message: '' } },
        { status: 401, statusText: 'Unauthorized' },
      );
    expect(auth.user()).toBeNull();

    auth.login('rose@example.org', 'plant lover').subscribe();
    const login = http.expectOne('/api/auth/login');
    expect(login.request.method).toBe('POST');
    expect(login.request.body).toEqual({ email: 'rose@example.org', password: 'plant lover' });
    login.flush({ data: USER });
    expect(auth.user()).toEqual(USER);

    auth.logout().subscribe();
    http.expectOne('/api/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
    expect(auth.user()).toBeNull();
  });

  // Protects against: a failed sign-in with no explanation, or signing in losing the page the user came from.
  it('signs in, shows the API message on failure, and returns to the requested page', async () => {
    const { harness, page } = await open('/login?returnUrl=%2Fplants%2Fabc');
    type(page, '#login-email', 'rose@example.org');
    type(page, '#login-password', 'wrong password');
    submit(page);
    http
      .expectOne('/api/auth/login')
      .flush(
        {
          error: {
            code: 'INVALID_CREDENTIALS',
            message: 'The email address or password is incorrect.',
          },
        },
        { status: 401, statusText: 'Unauthorized' },
      );
    let shown = await settle(harness);
    expect(shown.querySelector('[role="alert"]')?.textContent).toContain(
      'The email address or password is incorrect.',
    );

    type(shown, '#login-password', 'plant lover');
    submit(shown);
    http.expectOne('/api/auth/login').flush({ data: USER });
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe('/plants/abc');
    http.expectOne('/api/plants/abc').flush(null, { status: 404, statusText: 'Not Found' });
  });

  // Protects against: sending a form the API will refuse, or hiding which field was wrong.
  it('checks the sign-up form, shows field messages, then asks the new user to confirm their email', async () => {
    const { harness, page } = await open('/signup');
    type(page, '#signup-name', 'Rose Lover');
    type(page, '#signup-email', 'rose@example.org');
    type(page, '#signup-password', 'short');
    submit(page);
    let shown = await settle(harness);
    expect(shown.querySelector('#signup-password-error')?.textContent).toContain(
      'at least 8 characters',
    );

    type(shown, '#signup-password', 'plant lover');
    submit(shown);
    const register = http.expectOne('/api/auth/register');
    expect(register.request.body).toEqual({
      email: 'rose@example.org',
      password: 'plant lover',
      displayName: 'Rose Lover',
    });
    register.flush(
      {
        error: {
          code: 'EMAIL_TAKEN',
          message: 'An account with this email address already exists.',
        },
      },
      { status: 409, statusText: 'Conflict' },
    );
    shown = await settle(harness);
    expect(shown.querySelector('[role="alert"]')?.textContent).toContain('already exists');

    submit(shown);
    http.expectOne('/api/auth/register').flush({ data: USER });
    shown = await settle(harness);
    expect(shown.querySelector('h1')?.textContent).toBe('Check your email');
    expect(shown.textContent).toContain('rose@example.org');
  });

  // Protects against: the reset request revealing whether the address has an account.
  it('gives the same answer after asking for a reset link', async () => {
    const { harness, page } = await open('/forgot-password');
    type(page, '#forgot-email', 'someone@example.org');
    submit(page);
    http
      .expectOne('/api/auth/forgot-password')
      .flush({ data: { status: 'sent' } }, { status: 202, statusText: 'Accepted' });

    expect((await settle(harness)).querySelector('[role="status"]')?.textContent).toContain(
      'If an account uses that address',
    );
  });

  // Protects against: a reset page that cannot work (missing token) pretending to, or a reset with no confirmation.
  it('explains that a reset link without its token cannot work', async () => {
    const { page } = await open('/reset-password');
    expect(page.querySelector('[role="alert"]')?.textContent).toContain('incomplete');
  });

  it('resets the password from the emailed link', async () => {
    const { harness, page } = await open('/reset-password?email=rose%40example.org&token=abc');
    type(page, '#reset-password', 'new password');
    submit(page);
    const reset = http.expectOne('/api/auth/reset-password');
    expect(reset.request.body).toEqual({
      email: 'rose@example.org',
      token: 'abc',
      password: 'new password',
    });
    reset.flush(null, { status: 204, statusText: 'No Content' });

    expect((await settle(harness)).querySelector('[role="status"]')?.textContent).toContain(
      'changed',
    );
  });
});
