import { test, expect } from '@playwright/test';
import { BASE_URL } from '../helpers';

test('empty submit remains on /login', async ({ page }) => {
  await page.goto(`${BASE_URL}/login`, { waitUntil: 'networkidle' });

  // Click Sign In without filling any fields
  await page.click('button:has-text("Sign In")');

  // Should remain on login page
  await expect(page).toHaveURL(/\/login/);

  // Login form should still be visible
  await expect(page.locator('[placeholder="you@example.com"]')).toBeVisible();
  await expect(page.locator('[placeholder="Password"]')).toBeVisible();
});
