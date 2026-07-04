import { test, expect } from '@playwright/test';
import { login, BASE_URL, WORKSPACE_SESSION_URL } from '../helpers';

test('h1 reads "Position Description Builder" on both new and existing session pages', async ({ page }) => {
  await login(page);

  // Check heading on new workspace
  await page.goto(`${BASE_URL}/`, { waitUntil: 'load' });
  await expect(page.locator('h1')).toContainText('Position Description Builder');

  // Check heading on existing session
  await page.goto(WORKSPACE_SESSION_URL, { waitUntil: 'load' });
  await expect(page.locator('h1')).toContainText('Position Description Builder');
});
