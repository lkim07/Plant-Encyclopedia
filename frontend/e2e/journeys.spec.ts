import { expect, Page, test } from '@playwright/test';
import { confirmationLink, newEmail, photo, ROSE, THEME_KEY } from './support';

// The main journeys (QA pass 2026-10-27, now automated). Identification is answered by a stub in the browser, so no
// photo ever reaches Pl@ntNet.

type Card = { id: string; name: string; plantGroup: string | null; imageUrl: string | null };

async function explorePlants(page: Page): Promise<Card[]> {
  const response = await page.request.get('/api/explore?page=1&pageSize=20');
  return (await response.json()).data;
}

test.describe('phone', () => {
  test.use({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });

  test('Back to Explore keeps the scroll position', async ({ page }) => {
    await page.goto('/explore', { waitUntil: 'networkidle' });
    const cards = page.locator('ul[aria-label="Plants to discover"] li a.card');
    await expect(cards.nth(9)).toBeAttached();
    const count = await cards.count();
    await cards.nth(9).scrollIntoViewIfNeeded();
    const before = await page.evaluate(() => Math.round(scrollY));
    expect(before).toBeGreaterThan(300);

    await cards.nth(9).click();
    await page.waitForURL(/\/plants\//);
    await expect(page.locator('h1').first()).toBeVisible();
    await page.goBack();

    await expect(cards).toHaveCount(count);
    await expect.poll(() => page.evaluate(() => Math.round(scrollY))).toBeGreaterThan(before - 80);
    expect(Math.abs((await page.evaluate(() => Math.round(scrollY))) - before)).toBeLessThanOrEqual(
      80,
    );
  });

  test('Where to Buy falls back when location is refused, and a town can be chosen', async ({
    browser,
  }) => {
    const context = await browser.newContext({
      viewport: { width: 390, height: 844 },
      isMobile: true,
      hasTouch: true,
      ignoreHTTPSErrors: true,
      baseURL: test.info().project.use.baseURL,
      permissions: [],
    });
    const page = await context.newPage();
    await page.goto(`/plants/${ROSE.id}/where-to-buy`);

    await expect(page.getByText("We couldn't use your location")).toBeVisible();
    await page.locator('.location select').selectOption({ label: 'Kamloops, BC' });
    await expect(page.locator('.shop h3').first()).toContainText('(Kamloops)');
    await context.close();
  });
});

test('Where to Buy lists shops near the visitor, nearest first', async ({ browser }) => {
  const context = await browser.newContext({
    ignoreHTTPSErrors: true,
    baseURL: test.info().project.use.baseURL,
    geolocation: { latitude: 49.2827, longitude: -123.1207 },
    permissions: ['geolocation'],
  });
  const page = await context.newPage();
  await page.goto(`/plants/${ROSE.id}/where-to-buy`);

  const shops = page.locator('.shop h3');
  await expect(shops.first()).toBeVisible();
  expect(await shops.allTextContents()).toEqual(
    expect.arrayContaining([expect.stringContaining('(Vancouver)')]),
  );
  await expect(shops.first()).toContainText('(Vancouver)');
  await context.close();
});

test('search: suggestions, results and recent searches', async ({ page }) => {
  await page.goto('/search', { waitUntil: 'networkidle' });
  await page
    .getByRole('combobox', { name: 'Search plants' })
    .pressSequentially('ros', { delay: 50 });
  await expect(page.getByRole('listbox', { name: 'Suggestions' })).toBeVisible();
  await expect(page.getByRole('option').first()).toBeVisible();

  await page.keyboard.press('Enter');
  await page.waitForURL(/q=ros/);
  await expect(page.locator('main li a').first()).toBeVisible();

  await page.getByRole('button', { name: 'Clear search' }).click();
  await expect(page.locator('.recent-text', { hasText: /^ros$/ })).toBeVisible();
});

test('signed out, the bookmark opens the sign-in prompt and Esc closes it', async ({ page }) => {
  await page.goto(`/plants/${ROSE.id}`, { waitUntil: 'networkidle' });
  await page.getByRole('button', { name: `Save ${ROSE.name} to Favorites` }).click();
  await expect(page.locator('dialog[open]')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.locator('dialog[open]')).toHaveCount(0);
});

test('identification shows the results and More related plants', async ({ page }) => {
  const plants = (await explorePlants(page)).slice(0, 3);
  await page.route('**/api/identification', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        data: {
          status: 'completed',
          candidates: plants.map((p, i) => ({
            rank: i + 1,
            plant: {
              id: p.id,
              name: p.name,
              plantGroup: p.plantGroup,
              plantGroupId: null,
              scientificName: p.name,
              imageUrl: p.imageUrl,
            },
          })),
        },
      }),
    }),
  );

  await page.goto('/identify', { waitUntil: 'networkidle' });
  await page
    .locator('input[type="file"]')
    .nth(1)
    .setInputFiles(await photo(page));
  await page.getByRole('button', { name: 'Identify', exact: true }).click();
  await page.waitForURL(/identify\/results/);
  await expect(page.locator('.first .name')).toHaveText(plants[0].name);

  await page.getByRole('link', { name: 'More related plants' }).click();
  await page.waitForURL(/identify\/more/);
  expect(page.url()).toContain(plants[0].id);
  await expect(page.locator('ul[aria-label="Related plants"] li').first()).toBeVisible();
});

test('a photo that cannot be identified offers Try Again and Choose Another Photo', async ({
  page,
}) => {
  await page.route('**/api/identification', (route) =>
    route.fulfill({
      status: 422,
      contentType: 'application/json',
      body: JSON.stringify({ error: { code: 'IDENTIFICATION_FAILED', message: '' } }),
    }),
  );

  await page.goto('/identify', { waitUntil: 'networkidle' });
  await page
    .locator('input[type="file"]')
    .nth(1)
    .setInputFiles(await photo(page));
  await page.getByRole('button', { name: 'Identify', exact: true }).click();

  await expect(page.getByRole('alert')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Try Again' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Choose Another Photo' })).toBeVisible();
});

test('the theme switch is kept after a reload', async ({ page }) => {
  await page.goto('/explore', { waitUntil: 'networkidle' });
  const before = await page.evaluate(() => document.documentElement.dataset['theme']);
  await page
    .locator('aside')
    .getByRole('button', { name: /Switch to (dark|light) theme/ })
    .click();
  const after = await page.evaluate(() => document.documentElement.dataset['theme']);
  expect(after).not.toBe(before);
  expect(await page.evaluate((key) => localStorage.getItem(key), THEME_KEY)).toBe(after);

  await page.reload({ waitUntil: 'domcontentloaded' });
  expect(await page.evaluate(() => document.documentElement.dataset['theme'])).toBe(after);
});

test('the skip link is first in the tab order and moves focus to the content', async ({ page }) => {
  await page.goto('/explore', { waitUntil: 'networkidle' });
  await page.keyboard.press('Tab');
  await expect(page.locator(':focus')).toHaveText('Skip to main content');
  await page.keyboard.press('Enter');
  expect(await page.evaluate(() => document.activeElement?.tagName)).toBe('MAIN');
});

test.describe('reduced motion', () => {
  test.use({ reducedMotion: 'reduce' });

  test('titles are shown at once, without the rising copy', async ({ page }) => {
    await page.goto('/explore');
    await expect(page.locator('h1').first()).toBeVisible();
    await expect(page.locator('.title-rise-words')).toHaveCount(0);
    expect(
      await page
        .locator('h1')
        .first()
        .evaluate((h) => (h as HTMLElement).style.color),
    ).toBe('');
  });
});

test('account: sign up, confirm, save Favorites, arrange, remove and Undo', async ({ page }) => {
  const email = newEmail();
  const [, second] = (await explorePlants(page)).filter((p) => p.id !== ROSE.id);
  const names = () => page.locator('.card-grid .name').allTextContents();

  await page.goto('/signup', { waitUntil: 'networkidle' });
  await page.getByLabel('Name').fill('E2E Tester');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill('e2e plant tester 2026');
  await page.getByRole('button', { name: /Create account/ }).click();
  await expect(page.getByRole('heading', { name: 'Check your email' })).toBeVisible();

  await page.goto(await confirmationLink(email), { waitUntil: 'networkidle' });

  for (const plant of [ROSE, second]) {
    await page.goto(`/plants/${plant.id}`, { waitUntil: 'networkidle' });
    const bookmark = page.getByRole('button', { name: `Save ${plant.name} to Favorites` });
    // The button turns on at once; wait for the save itself before leaving the page.
    const saved = page.waitForResponse(
      (r) => r.url().endsWith('/api/favorites') && r.request().method() === 'POST',
    );
    await bookmark.click();
    expect((await saved).ok()).toBe(true);
    await expect(bookmark).toHaveAttribute('aria-pressed', 'true');
  }

  await page.goto('/favorites', { waitUntil: 'networkidle' });
  await expect(page.locator('.card-grid .name')).toHaveCount(2);

  await page.getByRole('button', { name: 'Arrange' }).click();
  await page.getByRole('button', { name: `Move ${second.name} earlier` }).click();
  const ordered = page.waitForResponse((r) => r.url().endsWith('/api/favorites/order'));
  await page.getByRole('button', { name: 'Done' }).click();
  expect((await ordered).ok()).toBe(true);
  await page.reload({ waitUntil: 'networkidle' });
  await expect(page.locator('.card-grid .name').first()).toHaveText(second.name);
  const arranged = await names();

  await page.getByRole('button', { name: `Save ${ROSE.name} to Favorites` }).click();
  const restored = page.waitForResponse(
    (r) => r.url().includes('/api/favorites') && r.request().method() !== 'GET',
  );
  await page.getByRole('button', { name: 'Undo' }).click();
  await expect(page.locator('.card-grid .name')).toHaveCount(2);
  await restored;
  await page.reload({ waitUntil: 'networkidle' });
  await expect(page.locator('.card-grid .name')).toHaveCount(2);
  expect(await names()).toEqual(arranged);
});
