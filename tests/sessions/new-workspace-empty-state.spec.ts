import { test, expect } from '@playwright/test';
import { login, BASE_URL } from '../helpers';

test('new workspace at / shows empty chat, no right editor, no Export button', async ({ page }) => {
  await login(page);
  await page.goto(`${BASE_URL}/`, { waitUntil: 'networkidle' });

  // Empty chat placeholder should be visible
  await expect(page.locator('.chat-empty, .empty-state, text=/start|begin|ask|type/i').first()).toBeVisible();

  // Right editor should NOT be in DOM
  await expect(page.locator('section.right-editor')).not.toBeAttached();

  // Export button should NOT be visible
  await expect(page.locator('button.ghost-btn:has-text("Export")')).not.toBeVisible();
});
