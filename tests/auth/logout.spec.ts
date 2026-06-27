import { test, expect } from '@playwright/test';
import { login, BASE_URL } from '../helpers';

test('logout redirects to /login and protects / afterwards', async ({ page }) => {
  await login(page);

  // Click the Sign out link
  await page.click('text=Sign out');
  await page.waitForURL(/\/login/, { timeout: 10_000 });
  await expect(page).toHaveURL(/\/login/);

  // Try to access the root while unauthenticated — should redirect back to /login
  await page.goto(`${BASE_URL}/`, { waitUntil: 'networkidle' });
  await expect(page).toHaveURL(/\/login/);
});
