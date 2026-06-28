import { test, expect } from '@playwright/test';
import { EMAIL, BASE_URL } from '../helpers';

test('register with existing email shows error and stays on /register', async ({ page }) => {
  await page.goto(`${BASE_URL}/register`, { waitUntil: 'load' });

  // Use an email that already exists
  await page.fill('[placeholder="you@example.com"]', EMAIL);
  await page.fill('[placeholder="At least 6 characters"]', 'Password123!');

  await page.click('button:has-text("Register"), button:has-text("Sign Up"), button[type="submit"]');

  // Should remain on /register
  await expect(page).toHaveURL(/\/register/);

  // Should show an error about duplicate email
  await expect(page.locator('text=/already|exists|taken|duplicate|registered/i').first()).toBeVisible({ timeout: 5_000 });
});
