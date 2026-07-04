import { test, expect } from '@playwright/test';
import { login, WORKSPACE_SESSION_URL } from '../helpers';

test('pressing Escape during rename reverts to original name', async ({ page }) => {
  await login(page);
  await page.goto(WORKSPACE_SESSION_URL, { waitUntil: 'load' });

  const sessionNameLocator = page.locator('.session-item--active .session-name');
  await expect(sessionNameLocator).toBeVisible({ timeout: 10_000 });
  const originalName = await sessionNameLocator.textContent() ?? '';

  // Double-click to enter rename mode
  await sessionNameLocator.dblclick();

  const renameInput = page.locator('.session-rename-input');
  await expect(renameInput).toBeVisible({ timeout: 5_000 });

  // Type some new text
  await renameInput.fill('');
  await renameInput.pressSequentially('This should not be saved', { delay: 30 });

  // Press Escape to cancel
  await renameInput.press('Escape');

  // Input should disappear and name should revert to original
  await expect(renameInput).not.toBeVisible({ timeout: 5_000 });
  await expect(sessionNameLocator).toContainText(originalName.trim(), { timeout: 5_000 });
});
