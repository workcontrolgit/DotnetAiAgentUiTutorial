import { test, expect } from '@playwright/test';
import { BASE_URL, EXISTING_SESSION_ID, WORKSPACE_SESSION_URL } from '../helpers';

test('unauthenticated access to workspace redirects to /login with ReturnUrl', async ({ page }) => {
  // Fresh page with no auth cookies
  await page.goto(WORKSPACE_SESSION_URL, { waitUntil: 'load' });

  // Should redirect to /login
  await expect(page).toHaveURL(/\/login/);

  // ReturnUrl should contain the workspace path
  const url = page.url();
  expect(url).toContain('ReturnUrl');
  expect(decodeURIComponent(url)).toContain(EXISTING_SESSION_ID);
});
