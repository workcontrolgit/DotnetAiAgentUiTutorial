import { test, expect } from '@playwright/test';
import { login, BASE_URL } from '../helpers';

test('clicking Send without typing does not navigate or create a message', async ({ page }) => {
  await login(page);
  await page.goto(`${BASE_URL}/`, { waitUntil: 'load' });

  // Click the Send button without typing anything
  await page.locator('button.primary-btn').click();

  // URL should remain at /
  await expect(page).toHaveURL(`${BASE_URL}/`);

  // No user message should appear
  await expect(page.locator('.chat-bubble-row--user .chat-bubble')).not.toBeVisible();
});
