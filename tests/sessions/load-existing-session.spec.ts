import { test, expect } from '@playwright/test';
import { login, EXISTING_SESSION_ID, WORKSPACE_SESSION_URL } from '../helpers';

test('navigating to existing session shows conversation history and highlights session', async ({ page }) => {
  await login(page);
  await page.goto(WORKSPACE_SESSION_URL, { waitUntil: 'load' });

  // Conversation history should be visible
  await expect(
    page.locator('.chat-bubble-row--assistant .chat-bubble:not(.chat-bubble--loading)').first()
  ).toBeVisible({ timeout: 15_000 });

  // The active session should be highlighted in the sidebar
  await expect(page.locator('.session-item--active')).toBeVisible();
});
