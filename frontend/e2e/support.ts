import { Page } from '@playwright/test';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, resolve } from 'node:path';

/** Sample data (backend/dev-data/seed-dev-data.sql): a published rose in the Roses group. */
export const ROSE = { id: 'd5e00000-0000-4000-8000-000000000001', name: "Rosa 'Peace'" };

/** Where the stack's API writes account emails (docker-compose.e2e.yml bind-mounts it). */
const EMAILS = process.env['E2E_EMAILS'] ?? resolve(__dirname, '../../.e2e/emails');

export const THEME_KEY = 'plant-encyclopedia.theme';

/** A new address per run, so the suite can run again on the same database. */
export function newEmail(): string {
  return `e2e-${Date.now()}-${Math.floor(Math.random() * 1e6)}@example.org`;
}

/** The confirmation link from the account email sent to the address, waiting for the file to appear. */
export async function confirmationLink(email: string, timeoutMs = 10_000): Promise<string> {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    for (const file of readdirSync(EMAILS)) {
      const path = join(EMAILS, file);
      if (!statSync(path).isFile()) continue;
      const text = readFileSync(path, 'utf8');
      if (!text.includes(email)) continue;
      const link = text.match(/https?:\/\/[^\s"'<>]*confirm[^\s"'<>]*/i)?.[0];
      if (link) return link.replace(/&amp;/g, '&');
    }
    await new Promise((r) => setTimeout(r, 200));
  }
  throw new Error(`No confirmation email for ${email} in ${EMAILS}`);
}

/** A real PNG to upload as a plant photo: a screenshot of a plain green page. */
export async function photo(
  page: Page,
): Promise<{ name: string; mimeType: string; buffer: Buffer }> {
  const blank = await page.context().newPage();
  await blank.setContent('<body style="margin:0;background:#3a7d44"></body>');
  await blank.setViewportSize({ width: 320, height: 240 });
  const buffer = await blank.screenshot({ type: 'png' });
  await blank.close();
  return { name: 'leaf.png', mimeType: 'image/png', buffer };
}
