import { test, expect } from '@playwright/test';
import { login, BASE_URL } from '../helpers';

test.setTimeout(150_000);

test('pressing Enter in chat input sends prompt and navigates to session', async ({ page }) => {
  await login(page);
  await page.goto(`${BASE_URL}/`, { waitUntil: 'load' });

  const prompt = 'what are the duties of an HR manager';
  const chatInput = page.locator('textarea.chat-input');

  await chatInput.pressSequentially(prompt, { delay: 50 });
  await page.waitForTimeout(600);

  // Press Enter to send
  await chatInput.press('Enter');

  // URL should change to a session workspace
  await page.waitForURL(/\/workspace\//, { timeout: 30_000 });
  await expect(page).toHaveURL(/\/workspace\//);

  // User message should appear
  await expect(page.locator('.chat-bubble-row--user .chat-bubble').first()).toBeVisible({ timeout: 10_000 });
  await expect(page.locator('.chat-bubble-row--user .chat-bubble').first()).toContainText(prompt);
});
