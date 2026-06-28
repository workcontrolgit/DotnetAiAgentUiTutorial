import { test, expect } from '@playwright/test';
import { login, WORKSPACE_SESSION_URL } from '../helpers';

// NOTE: This test requires EXISTING_SESSION_ID to have a visible draft panel.
// If section.right-editor is not visible, the test will be skipped.

test('Export > Markdown triggers a .md file download', async ({ page }) => {
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

  await expect(page.locator('.export-menu')).toBeVisible({ timeout: 3_000 });

  // Set up download listener before clicking
  const downloadPromise = page.waitForEvent('download', { timeout: 15_000 });

  // Click Markdown option
  await page.locator('.export-menu').locator('text=/Markdown|\.md/i').first().click();

  const download = await downloadPromise;
  const filename = download.suggestedFilename();
  expect(filename.toLowerCase()).toMatch(/\.md$/);
});
