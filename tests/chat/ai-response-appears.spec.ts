import { test, expect } from '@playwright/test';
import { login, WORKSPACE_SESSION_URL } from '../helpers';

test('existing session shows assistant response bubble without loading indicator', async ({ page }) => {
  await login(page);
  await page.goto(WORKSPACE_SESSION_URL, { waitUntil: 'networkidle' });

  // Assistant response should be visible
  await expect(
    page.locator('.chat-bubble-row--assistant .chat-bubble:not(.chat-bubble--loading)').first()
  ).toBeVisible({ timeout: 15_000 });

  // Loading indicator should NOT be visible (no active AI call)
  await expect(page.locator('.chat-bubble--loading')).not.toBeVisible();
});
