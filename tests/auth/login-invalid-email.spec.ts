import { test, expect } from '@playwright/test';
import { BASE_URL } from '../helpers';

test('non-existent email stays on /login and shows error', async ({ page }) => {
  await page.goto(`${BASE_URL}/login`, { waitUntil: 'networkidle' });
  await page.fill('[placeholder="you@example.com"]', 'nobody@doesnotexist.invalid');
  await page.fill('[placeholder="Password"]', 'SomePassword1!');
  await page.click('button:has-text("Sign In")');

  // Should remain on login page
  await expect(page).toHaveURL(/\/login/);

  // Should show an error message
  await expect(page.locator('text=/invalid|incorrect|failed|error|not found/i').first()).toBeVisible({ timeout: 5_000 });
});
