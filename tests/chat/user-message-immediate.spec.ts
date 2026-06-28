import { test, expect } from '@playwright/test';
import { login, WORKSPACE_SESSION_URL } from '../helpers';

test.setTimeout(150_000);

test('user message appears immediately after clicking Send before AI responds', async ({ page }) => {
  await login(page);
  await page.goto(WORKSPACE_SESSION_URL, { waitUntil: 'load' });

  const prompt = 'describe recruitment briefly';
  const chatInput = page.locator('textarea.chat-input');

  await chatInput.pressSequentially(prompt, { delay: 50 });
  await page.waitForTimeout(600);

  // Click Send
  await page.locator('button.primary-btn').click();

  // User message should appear immediately (without waiting for AI)
  await expect(page.locator('.chat-bubble-row--user .chat-bubble').last()).toBeVisible({ timeout: 5_000 });
  await expect(page.locator('.chat-bubble-row--user .chat-bubble').last()).toContainText(prompt);

  // Loading indicator should be present (AI is still responding)
  await expect(page.locator('.chat-bubble--loading')).toBeVisible({ timeout: 10_000 });
});
