import { test, expect } from '@playwright/test';
import { login, BASE_URL } from '../helpers';

test.setTimeout(150_000);

test('typing a prompt creates a new session and user message appears', async ({ page }) => {
  await login(page);
  await page.goto(`${BASE_URL}/`, { waitUntil: 'networkidle' });

  const prompt = 'what is a job description';

  // Type into the chat textarea
  await page.locator('textarea.chat-input').pressSequentially(prompt, { delay: 50 });
  await page.waitForTimeout(600);

  // Click Send
  await page.locator('button.primary-btn').click();

  // URL should change from / to a session workspace URL
  await page.waitForURL(/\/workspace\//, { timeout: 30_000 });
  await expect(page).toHaveURL(/\/workspace\//);

  // User message should appear immediately
  await expect(page.locator('.chat-bubble-row--user .chat-bubble').first()).toBeVisible({ timeout: 10_000 });
  await expect(page.locator('.chat-bubble-row--user .chat-bubble').first()).toContainText(prompt);
});
