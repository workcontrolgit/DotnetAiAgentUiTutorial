import { test, expect } from '@playwright/test';

test('test', async ({ page }) => {
  await page.goto('https://medium.com/');
  await page.getByRole('link', { name: 'Sign in' }).click();
});