import { test, expect } from '@playwright/test';
import { login, WORKSPACE_SESSION_URL } from '../helpers';

// NOTE: This test requires EXISTING_SESSION_ID to have a visible draft panel.
// If section.right-editor is not visible, the test will be skipped.

test('clicking Export button again closes the export menu', async ({ page }) => {
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

  // Open menu
  await exportBtn.click();
  await expect(page.locator('.export-menu')).toBeVisible({ timeout: 3_000 });

  // Click Export again to close menu
  await exportBtn.click();

  // Menu should be closed
  await expect(page.locator('.export-menu')).not.toBeVisible({ timeout: 3_000 });
});
