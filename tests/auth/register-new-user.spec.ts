import { test, expect } from '@playwright/test';
import { BASE_URL } from '../helpers';

test('register new user with unique email logs in and lands at /', async ({ page }) => {
  const uniqueEmail = `testuser+${Date.now()}@example.com`;
  const password = 'Password123!';

  await page.goto(`${BASE_URL}/register`, { waitUntil: 'load' });

  // Fill registration form
  await page.fill('[placeholder="you@example.com"]', uniqueEmail);
  await page.fill('[placeholder="At least 6 characters"]', password);

  // Submit registration
  await page.click('button:has-text("Register"), button:has-text("Sign Up"), button[type="submit"]');

  // Should be logged in and at the root workspace
  await page.waitForURL(/\/(?!register|login)/, { timeout: 15_000 });
  await expect(page).not.toHaveURL(/\/register/);
  await expect(page).not.toHaveURL(/\/login/);
  await expect(page.locator('h1')).toContainText('Position Description Builder');
});
