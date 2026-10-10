import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';

describe('App routes', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter(routes)] });
  });

  // Protects against: the home URL not opening Identify (DEVELOPMENT_ROADMAP "Home / Identify").
  it('redirects / to /identify', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/');

    expect(TestBed.inject(Router).url).toBe('/identify');
    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe('Identify');
  });

  // Protects against: unknown URLs showing a blank page instead of a recoverable one.
  it('shows the not-found page for unknown URLs', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/no-such-page');

    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe('Page not found');
  });
});
