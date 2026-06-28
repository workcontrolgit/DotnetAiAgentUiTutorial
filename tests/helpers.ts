import { Page } from '@playwright/test';

export const EMAIL    = process.env.TEST_EMAIL    ?? '';
export const PASSWORD = process.env.TEST_PASSWORD ?? '';
export const BASE_URL = process.env.TEST_BASE_URL ?? 'http://localhost:5000';
export const EXISTING_SESSION_ID = process.env.TEST_SESSION_ID ?? '';
export const WORKSPACE_SESSION_URL = `${BASE_URL}/workspace/${EXISTING_SESSION_ID}`;

export async function login(page: Page) {
  await page.goto(`${BASE_URL}/login`, { waitUntil: 'load' });
  await page.fill('[placeholder="you@example.com"]', EMAIL);
  await page.fill('[placeholder="Password"]', PASSWORD);
  await page.click('button:has-text("Sign In")');
  await page.waitForURL(/\/(?!login)/, { timeout: 10_000 });
}
