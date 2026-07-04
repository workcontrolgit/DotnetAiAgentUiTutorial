import { test, expect } from '@playwright/test';
import { EMAIL, BASE_URL } from '../helpers';

test('wrong password stays on /login and shows error', async ({ page }) => {
  await page.goto(`${BASE_URL}/login`, { waitUntil: 'load' });
  await page.fill('[placeholder="you@example.com"]', EMAIL);
  await page.fill('[placeholder="Password"]', 'wrongpassword123');
  await page.click('button:has-text("Sign In")');

  // Should remain on login page
  await expect(page).toHaveURL(/\/login/);

  // Should show an error message
  await expect(page.locator('text=/invalid|incorrect|failed|error/i').first()).toBeVisible({ timeout: 5_000 });
});
