import { test, expect } from '@playwright/test';
import { login, BASE_URL } from '../helpers';

test('right editor panel is not in DOM and Export button is not visible on new workspace', async ({ page }) => {
  await login(page);
  await page.goto(`${BASE_URL}/`, { waitUntil: 'load' });

  // section.right-editor should NOT be attached to the DOM
  await expect(page.locator('section.right-editor')).not.toBeAttached();

  // Export button should NOT be visible
  await expect(page.locator('button.ghost-btn:has-text("Export")')).not.toBeVisible();
});
