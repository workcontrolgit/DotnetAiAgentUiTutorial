import { test, expect } from '@playwright/test';
import { login, BASE_URL, WORKSPACE_SESSION_URL } from '../helpers';

test('sidebar remains visible when navigating between new workspace and existing session', async ({ page }) => {
  await login(page);

  // Navigate to root workspace
  await page.goto(`${BASE_URL}/`, { waitUntil: 'networkidle' });
  await expect(page.locator('.sessions-sidebar')).toBeVisible();

  // Navigate to existing session
  await page.goto(WORKSPACE_SESSION_URL, { waitUntil: 'networkidle' });
  await expect(page.locator('.sessions-sidebar')).toBeVisible();
});
