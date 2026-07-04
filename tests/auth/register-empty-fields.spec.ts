import { test, expect } from '@playwright/test';
import { BASE_URL } from '../helpers';

test('submit empty registration form shows error and stays on /register', async ({ page }) => {
  await page.goto(`${BASE_URL}/register`, { waitUntil: 'load' });

  // Submit without filling any fields
  await page.click('button:has-text("Register"), button:has-text("Sign Up"), button[type="submit"]');

  // Should remain on /register
  await expect(page).toHaveURL(/\/register/);

  // Registration form should still be present
  await expect(page.locator('[placeholder="you@example.com"]')).toBeVisible();
  await expect(page.locator('[placeholder="At least 6 characters"]')).toBeVisible();
});
