import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { ROSE, THEME_KEY } from './support';

// Every page, signed out, at phone and desktop width in both themes (QA pass 2026-10-27, now automated): no WCAG 2.1
// A/AA violations, no sideways scrolling, the chosen theme applied, no page errors or failed requests.
const PAGES: [string, string][] = [
  ['identify', '/identify'],
  ['explore', '/explore'],
  ['search', '/search'],
  ['search results', '/search?q=rose'],
  ['category', '/categories/flowers'],
  ['plant group', '/plant-groups/roses'],
  ['plant', `/plants/${ROSE.id}`],
  ['where to buy', `/plants/${ROSE.id}/where-to-buy`],
  ['favorites', '/favorites'],
  ['login', '/login'],
  ['signup', '/signup'],
  ['forgot password', '/forgot-password'],
  ['not found', '/no-such-page'],
];

const VIEWPORTS = {
  phone: { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true },
  desktop: { viewport: { width: 1280, height: 860 } },
};

for (const [size, device] of Object.entries(VIEWPORTS)) {
  for (const theme of ['light', 'dark'] as const) {
    test.describe(`${size}, ${theme}`, () => {
      test.use({
        ...device,
        // Titles and cards appear at once, so the scan never sees a half-finished animation.
        reducedMotion: 'reduce',
        geolocation: { latitude: 49.2827, longitude: -123.1207 },
        permissions: ['geolocation'],
      });

      for (const [name, path] of PAGES) {
        test(name, async ({ page }) => {
          await page.addInitScript(
            ([key, value]) => localStorage.setItem(key, value),
            [THEME_KEY, theme],
          );
          const problems: string[] = [];
          page.on('pageerror', (e) => problems.push(`page error: ${e.message}`));
          page.on('response', (r) => {
            // Signed out, GET /api/me answers 401 by design.
            if (r.status() >= 400 && !(r.url().endsWith('/api/me') && r.status() === 401)) {
              problems.push(`HTTP ${r.status()} ${r.url()}`);
            }
          });
          page.on('requestfailed', (r) =>
            problems.push(`failed ${r.url()} ${r.failure()?.errorText}`),
          );

          await page.goto(path, { waitUntil: 'networkidle' });
          await expect(page.locator('h1').first()).toBeVisible();

          expect(await page.evaluate(() => document.documentElement.dataset['theme'])).toBe(theme);
          expect(
            await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth),
          ).toBeLessThanOrEqual(0);
          const axe = await new AxeBuilder({ page })
            .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
            .analyze();
          expect(
            axe.violations.map(
              (v) => `${v.id}: ${v.nodes.map((n) => n.target.join(' ')).join(', ')}`,
            ),
          ).toEqual([]);
          expect(problems).toEqual([]);
        });
      }
    });
  }
}
