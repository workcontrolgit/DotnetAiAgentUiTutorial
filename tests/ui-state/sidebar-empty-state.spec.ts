import { test, expect } from '@playwright/test';
import { BASE_URL } from '../helpers';

test('fresh account shows empty sessions state in sidebar', async ({ page }) => {
  const uniqueEmail = `freshuser+${Date.now()}@example.com`;
  const password = 'Password123!';

  // Register a new account
  await page.goto(`${BASE_URL}/register`, { waitUntil: 'load' });
  await page.fill('[placeholder="you@example.com"]', uniqueEmail);
  await page.fill('[placeholder="At least 6 characters"]', password);
  await page.click('button:has-text("Register"), button:has-text("Sign Up"), button[type="submit"]');

  // Wait to be logged in
  await page.waitForURL(/\/(?!register|login)/, { timeout: 15_000 });

  // Empty state message should be visible
  const emptyState = page.locator('.sessions-empty');
  await expect(emptyState).toBeVisible({ timeout: 5_000 });
  await expect(emptyState).toContainText('No conversations yet.');
});
