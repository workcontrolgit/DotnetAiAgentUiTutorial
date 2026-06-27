import { test, expect } from '@playwright/test';
import { login, BASE_URL } from '../helpers';

test('navigating to invalid session ID redirects to /', async ({ page }) => {
  await login(page);

  const invalidSessionUrl = `${BASE_URL}/workspace/00000000-0000-0000-0000-000000000000`;
  await page.goto(invalidSessionUrl, { waitUntil: 'networkidle' });

  // Should redirect to the root workspace
  await expect(page).toHaveURL(`${BASE_URL}/`);
});
