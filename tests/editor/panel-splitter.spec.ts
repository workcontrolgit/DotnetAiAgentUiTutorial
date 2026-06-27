import { test, expect } from '@playwright/test';
import { login, WORKSPACE_SESSION_URL } from '../helpers';

// NOTE: This test requires EXISTING_SESSION_ID to have a visible draft panel.
// If section.right-editor is not visible, the test will be skipped.

test('splitter is visible when right editor panel is open', async ({ page }) => {
  await login(page);
  await page.goto(WORKSPACE_SESSION_URL, { waitUntil: 'networkidle' });

  // Check if right editor is visible — skip if not
  const rightEditor = page.locator('section.right-editor');
  const isEditorVisible = await rightEditor.isVisible().catch(() => false);

  if (!isEditorVisible) {
    test.skip(true, 'EXISTING_SESSION_ID does not have a visible draft panel');
    return;
  }

  // Splitter should be present
  const splitter = page.locator('div.splitter[role="separator"]');
  await expect(splitter).toBeVisible();
});
