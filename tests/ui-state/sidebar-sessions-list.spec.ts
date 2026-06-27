import { test, expect } from '@playwright/test';
import { login, BASE_URL } from '../helpers';

test('sidebar shows at least one session item with name and delete button', async ({ page }) => {
  await login(page);
  await page.goto(`${BASE_URL}/`, { waitUntil: 'networkidle' });

  // Sidebar should be visible
  await expect(page.locator('.sessions-sidebar')).toBeVisible();

  // At least one session item should be present
  const firstSession = page.locator('.session-item').first();
  await expect(firstSession).toBeVisible({ timeout: 10_000 });

  // Session item should contain a session name
  await expect(firstSession.locator('.session-name')).toBeVisible();

  // Session item should have a delete button
  await expect(firstSession.locator('button.ghost-btn--icon[title="Delete"]')).toBeVisible();
});
