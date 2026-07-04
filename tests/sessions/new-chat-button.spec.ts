import { test, expect } from '@playwright/test';
import { login, BASE_URL, WORKSPACE_SESSION_URL } from '../helpers';

test('clicking + New button navigates to /', async ({ page }) => {
  await login(page);
  await page.goto(WORKSPACE_SESSION_URL, { waitUntil: 'load' });

  // Click + New button
  await page.click('button:has-text("+ New")');

  // Should navigate to / (new empty workspace)
  await page.waitForURL(`${BASE_URL}/`, { timeout: 10_000 });
  await expect(page).toHaveURL(`${BASE_URL}/`);
});
