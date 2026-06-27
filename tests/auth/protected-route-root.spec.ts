import { test, expect } from '@playwright/test';
import { BASE_URL } from '../helpers';

test('unauthenticated access to / redirects to /login with ReturnUrl', async ({ page }) => {
  // Fresh page with no auth cookies
  await page.goto(`${BASE_URL}/`, { waitUntil: 'networkidle' });

  // Should redirect to /login with ReturnUrl=%2F
  await expect(page).toHaveURL(/\/login/);
  expect(page.url()).toContain('ReturnUrl');
});
