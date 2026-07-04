import { test, expect } from '@playwright/test';
import { login, WORKSPACE_SESSION_URL } from '../helpers';

test('double-clicking session name allows renaming and Enter saves the new name', async ({ page }) => {
  await login(page);
  await page.goto(WORKSPACE_SESSION_URL, { waitUntil: 'load' });

  // Get the current session name
  const sessionNameLocator = page.locator('.session-item--active .session-name');
  await expect(sessionNameLocator).toBeVisible({ timeout: 10_000 });
  const originalName = await sessionNameLocator.textContent() ?? '';

  const newName = `Renamed Session ${Date.now()}`;

  // Double-click to enter rename mode
  await sessionNameLocator.dblclick();

  // A rename input should appear
  const renameInput = page.locator('.session-rename-input');
  await expect(renameInput).toBeVisible({ timeout: 5_000 });

  // Clear and type new name
  await renameInput.fill('');
  await renameInput.pressSequentially(newName, { delay: 30 });

  // Press Enter to save
  await renameInput.press('Enter');

  // Input should disappear and name should be updated
  await expect(renameInput).not.toBeVisible({ timeout: 5_000 });
  await expect(sessionNameLocator).toContainText(newName, { timeout: 5_000 });
});
