import { test, expect } from '@playwright/test';
import { login, WORKSPACE_SESSION_URL } from '../helpers';

// NOTE: This test requires EXISTING_SESSION_ID to have a visible draft panel.
// If section.right-editor is not visible, the test will be skipped.

test('clicking Export button opens export menu with Word, Markdown, and JSON options', async ({ page }) => {
  await login(page);
  await page.goto(WORKSPACE_SESSION_URL, { waitUntil: 'load' });

  // Check if right editor is visible — skip if not
  const rightEditor = page.locator('section.right-editor');
  const isEditorVisible = await rightEditor.isVisible().catch(() => false);

  if (!isEditorVisible) {
    test.skip(true, 'EXISTING_SESSION_ID does not have a visible draft panel');
    return;
  }

  const exportBtn = page.locator('button.ghost-btn:has-text("Export")');
  await expect(exportBtn).toBeVisible();
  await exportBtn.click();

  // Export menu should appear
  const exportMenu = page.locator('.export-menu');
  await expect(exportMenu).toBeVisible({ timeout: 3_000 });

  // Menu should contain Word, Markdown, and JSON options
  await expect(exportMenu.locator('text=/Word|\.docx/i').first()).toBeVisible();
  await expect(exportMenu.locator('text=/Markdown|\.md/i').first()).toBeVisible();
  await expect(exportMenu.locator('text=/JSON|\.json/i').first()).toBeVisible();
});
