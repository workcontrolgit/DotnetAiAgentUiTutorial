import { test, expect } from '@playwright/test';
import { BASE_URL } from '../helpers';

test('register with 3-char password shows validation error on /register', async ({ page }) => {
  await page.goto(`${BASE_URL}/register`, { waitUntil: 'networkidle' });

  await page.fill('[placeholder="you@example.com"]', `shortpass+${Date.now()}@example.com`);
  await page.fill('[placeholder="Password"]', 'abc');

  await page.click('button:has-text("Register"), button:has-text("Sign Up"), button[type="submit"]');

  // Should remain on /register
  await expect(page).toHaveURL(/\/register/);

  // Should show a validation error about password length or requirements
  await expect(page.locator('text=/password|short|length|characters|requirement/i').first()).toBeVisible({ timeout: 5_000 });
});
