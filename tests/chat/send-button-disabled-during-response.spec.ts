import { test, expect } from '@playwright/test';
import { login, WORKSPACE_SESSION_URL } from '../helpers';

test.setTimeout(150_000);

test('Send button is disabled while loading indicator is visible', async ({ page }) => {
  await login(page);
  await page.goto(WORKSPACE_SESSION_URL, { waitUntil: 'load' });

  const chatInput = page.locator('textarea.chat-input');
  await chatInput.pressSequentially('briefly describe performance reviews', { delay: 50 });
  await page.waitForTimeout(600);

  // Click Send
  await page.locator('button.primary-btn').click();

  // Wait for loading indicator to appear
  await expect(page.locator('.chat-bubble--loading')).toBeVisible({ timeout: 10_000 });

  // While loading indicator is visible, the Send button should be disabled
  await expect(page.locator('button.primary-btn[disabled]')).toBeVisible();
});
