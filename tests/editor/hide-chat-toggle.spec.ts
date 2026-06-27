import { test, expect } from '@playwright/test';
import { login, WORKSPACE_SESSION_URL } from '../helpers';

// NOTE: This test requires EXISTING_SESSION_ID to have a visible draft panel.
// If section.right-editor is not visible, the test will be skipped.

test('Hide Chat button toggles chat panel visibility', async ({ page }) => {
  await login(page);
  await page.goto(WORKSPACE_SESSION_URL, { waitUntil: 'networkidle' });

  // Check if right editor is visible — skip if not
  const rightEditor = page.locator('section.right-editor');
  const isEditorVisible = await rightEditor.isVisible().catch(() => false);

  if (!isEditorVisible) {
    test.skip(true, 'EXISTING_SESSION_ID does not have a visible draft panel');
    return;
  }

  // Find and click Hide Chat button
  const hideChatBtn = page.locator('button:has-text("Hide Chat")');
  await expect(hideChatBtn).toBeVisible();
  await hideChatBtn.click();

  // Chat section should be hidden
  await expect(page.locator('section.left-chat, .chat-section, .chat-panel')).not.toBeVisible({ timeout: 3_000 });

  // Button should now say "Show Chat"
  const showChatBtn = page.locator('button:has-text("Show Chat")');
  await expect(showChatBtn).toBeVisible();

  // Click again to show chat
  await showChatBtn.click();

  // Chat section should be visible again
  await expect(page.locator('textarea.chat-input')).toBeVisible({ timeout: 3_000 });

  // Button should revert to "Hide Chat"
  await expect(page.locator('button:has-text("Hide Chat")')).toBeVisible();
});
