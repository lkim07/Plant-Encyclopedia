import { defineConfig, devices } from '@playwright/test';

/**
 * End-to-end tests (design 2026-10-27) against the Docker stack:
 *   docker compose -f docker-compose.e2e.yml up --build --wait   (from the repo root)
 *   npm run e2e                                                  (from frontend/)
 * E2E_BASE_URL points them elsewhere; E2E_EMAILS is where the API writes account emails.
 */
export default defineConfig({
  testDir: '.',
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  reporter: process.env['CI']
    ? [['list'], ['html', { open: 'never', outputFolder: '../playwright-report' }]]
    : 'list',
  outputDir: '../test-results',
  use: {
    baseURL: process.env['E2E_BASE_URL'] ?? 'https://localhost:8443',
    ignoreHTTPSErrors: true, // the stack's throwaway self-signed certificate
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1280, height: 860 } },
    },
  ],
});
