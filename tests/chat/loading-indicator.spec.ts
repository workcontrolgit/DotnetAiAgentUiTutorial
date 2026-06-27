import { test, expect } from '@playwright/test';
import { login, WORKSPACE_SESSION_URL } from '../helpers';

test.setTimeout(150_000);

test('loading indicator appears while AI is responding', async ({ page }) => {
  await login(page);
  await page.goto(WORKSPACE_SESSION_URL, { waitUntil: 'networkidle' });

  const chatInput = page.locator('textarea.chat-input');
  await chatInput.pressSequentially('briefly explain onboarding', { delay: 50 });
  await page.waitForTimeout(600);

  // Click Send
  await page.locator('button.primary-btn').click();

  // Loading indicator should appear
  await expect(page.locator('.chat-bubble--loading')).toBeVisible({ timeout: 10_000 });
});
