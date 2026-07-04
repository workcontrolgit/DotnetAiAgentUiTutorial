import { test, expect } from '@playwright/test';
import { login, BASE_URL } from '../helpers';

test('help icon has title attribute containing "Enter to send"', async ({ page }) => {
  await login(page);
  await page.goto(`${BASE_URL}/`, { waitUntil: 'load' });

  const helpIcon = page.locator('span.help-icon');
  await expect(helpIcon).toBeVisible();

  const title = await helpIcon.getAttribute('title');
  expect(title).not.toBeNull();
  expect(title!.toLowerCase()).toContain('enter to send');
});
