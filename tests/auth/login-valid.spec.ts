import { test, expect, Page } from '@playwright/test';
import { login, BASE_URL } from '../helpers';

test('login with valid credentials shows workspace UI elements', async ({ page }) => {
  await login(page);

  // Verify we are not on the login page
  await expect(page).not.toHaveURL(/\/login/);

  // Verify core workspace UI elements are present
  await expect(page.locator('h1')).toContainText('Position Description Builder');
  await expect(page.locator('.sessions-sidebar')).toBeVisible();
  await expect(page.locator('textarea.chat-input')).toBeVisible();
  await expect(page.locator('button:has-text("+ New")')).toBeVisible();
});
