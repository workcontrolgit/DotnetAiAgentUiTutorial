import { test, expect } from '@playwright/test';
import { login, BASE_URL } from '../helpers';

test.setTimeout(150_000);

test('textarea is cleared immediately after clicking Send', async ({ page }) => {
  await login(page);
  await page.goto(`${BASE_URL}/`, { waitUntil: 'networkidle' });

  const chatInput = page.locator('textarea.chat-input');
  const typedText = 'this should be cleared after sending';

  await chatInput.pressSequentially(typedText, { delay: 50 });
  await page.waitForTimeout(600);

  // Verify text is in textarea before sending
  await expect(chatInput).toHaveValue(typedText);

  // Click Send
  await page.locator('button.primary-btn').click();

  // Textarea should be empty immediately after send
  await expect(chatInput).toHaveValue('', { timeout: 3_000 });
});
