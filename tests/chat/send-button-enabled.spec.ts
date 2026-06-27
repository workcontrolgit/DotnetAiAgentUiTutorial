import { test, expect } from '@playwright/test';
import { login, BASE_URL } from '../helpers';

test('Send button becomes enabled after typing text into chat input', async ({ page }) => {
  await login(page);
  await page.goto(`${BASE_URL}/`, { waitUntil: 'networkidle' });

  const sendButton = page.locator('button.primary-btn');
  const chatInput = page.locator('textarea.chat-input');

  // Initially the button may be disabled (no text)
  await expect(chatInput).toBeVisible();

  // Type text into the chat input
  await chatInput.pressSequentially('Hello there', { delay: 50 });

  // Send button should now be enabled
  await expect(page.locator('button.primary-btn:not([disabled])')).toBeVisible({ timeout: 3_000 });
});
