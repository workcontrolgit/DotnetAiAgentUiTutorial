import { test, expect } from '@playwright/test';
import { login, WORKSPACE_SESSION_URL } from '../helpers';

// NOTE: This test requires EXISTING_SESSION_ID to have a visible draft panel.
// If section.right-editor is not visible, the test will be skipped.

test('clicking Quill editor and typing text inserts it into the editor', async ({ page }) => {
  await login(page);
  await page.goto(WORKSPACE_SESSION_URL, { waitUntil: 'networkidle' });

  // Check if right editor is visible — skip if not
  const rightEditor = page.locator('section.right-editor');
  const isEditorVisible = await rightEditor.isVisible().catch(() => false);

  if (!isEditorVisible) {
    test.skip(true, 'EXISTING_SESSION_ID does not have a visible draft panel');
    return;
  }

  const quillEditor = page.locator('#quill-editor-wrapper .ql-editor');
  await expect(quillEditor).toBeVisible();

  // Click to focus
  await quillEditor.click();

  // Type some text
  const typedText = 'Direct edit test content';
  await quillEditor.pressSequentially(typedText, { delay: 30 });

  // Verify text appears in the editor
  await expect(quillEditor).toContainText(typedText);
});
