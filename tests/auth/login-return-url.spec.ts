import { test, expect } from '@playwright/test';
import { EMAIL, PASSWORD, BASE_URL, EXISTING_SESSION_ID, WORKSPACE_SESSION_URL } from '../helpers';

test('login with ReturnUrl redirects back to session and loads history', async ({ page }) => {
  // Navigate to session URL while unauthenticated to set ReturnUrl
  await page.goto(WORKSPACE_SESSION_URL, { waitUntil: 'networkidle' });
  await expect(page).toHaveURL(/\/login/);

  // Login
  await page.fill('[placeholder="you@example.com"]', EMAIL);
  await page.fill('[placeholder="Password"]', PASSWORD);
  await page.click('button:has-text("Sign In")');

  // Should be redirected to the original session URL
  await page.waitForURL(new RegExp(EXISTING_SESSION_ID), { timeout: 10_000 });
  await expect(page).toHaveURL(new RegExp(EXISTING_SESSION_ID));

  // Conversation history should load
  await expect(page.locator('.chat-bubble-row--assistant .chat-bubble:not(.chat-bubble--loading)').first()).toBeVisible({ timeout: 15_000 });
});
