import { Page } from '@playwright/test';

export const EMAIL = 'fuji.nguyen@workcontrol.com';
export const PASSWORD = 'gasoline87';
export const BASE_URL = 'http://localhost:5000';
export const EXISTING_SESSION_ID = 'e57987e2-d833-456e-ab6d-06f3ea8bbe9f';
export const WORKSPACE_SESSION_URL = `${BASE_URL}/workspace/${EXISTING_SESSION_ID}`;

export async function login(page: Page) {
  await page.goto(`${BASE_URL}/login`, { waitUntil: 'networkidle' });
  await page.fill('[placeholder="you@example.com"]', EMAIL);
  await page.fill('[placeholder="Password"]', PASSWORD);
  await page.click('button:has-text("Sign In")');
  await page.waitForURL(/\/(?!login)/, { timeout: 10_000 });
}
