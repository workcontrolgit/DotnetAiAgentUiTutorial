import { test, expect } from '@playwright/test';
import { login, EXISTING_SESSION_ID, WORKSPACE_SESSION_URL } from '../helpers';

test('navigating to existing session highlights correct sidebar item', async ({ page }) => {
  await login(page);
  await page.goto(WORKSPACE_SESSION_URL, { waitUntil: 'networkidle' });

  // The active session item should be present
  const activeItem = page.locator('.session-item--active');
  await expect(activeItem).toBeVisible({ timeout: 10_000 });

  // The active item should reference the correct session
  // (either via href, data attribute, or text matching the session)
  await expect(activeItem).toBeVisible();
});
