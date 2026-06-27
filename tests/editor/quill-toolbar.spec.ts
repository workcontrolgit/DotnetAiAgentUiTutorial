import { test, expect } from '@playwright/test';
import { login, WORKSPACE_SESSION_URL } from '../helpers';

// NOTE: This test requires EXISTING_SESSION_ID to have a visible draft panel.
// If section.right-editor is not visible, the test will be skipped.

test('Quill toolbar is visible with bold, italic, and list buttons', async ({ page }) => {
  await login(page);
  await page.goto(WORKSPACE_SESSION_URL, { waitUntil: 'networkidle' });

  // Check if right editor is visible — skip if not
  const rightEditor = page.locator('section.right-editor');
  const isEditorVisible = await rightEditor.isVisible().catch(() => false);

  if (!isEditorVisible) {
    test.skip(true, 'EXISTING_SESSION_ID does not have a visible draft panel');
    return;
  }

  // Quill toolbar should be visible
  const toolbar = page.locator('.ql-toolbar');
  await expect(toolbar).toBeVisible();

  // Bold button
  await expect(toolbar.locator('.ql-bold')).toBeVisible();

  // Italic button
  await expect(toolbar.locator('.ql-italic')).toBeVisible();

  // List button (ordered or unordered)
  await expect(toolbar.locator('.ql-list').first()).toBeVisible();
});
