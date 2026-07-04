import { test, expect } from '@playwright/test';
import { login, BASE_URL } from '../helpers';

test('Shift+Enter inserts a newline without sending the message', async ({ page }) => {
  await login(page);
  await page.goto(`${BASE_URL}/`, { waitUntil: 'load' });

  const chatInput = page.locator('textarea.chat-input');

  // Type first line
  await chatInput.pressSequentially('First line', { delay: 50 });

  // Shift+Enter should insert newline
  await chatInput.press('Shift+Enter');

  // Type second line
  await chatInput.pressSequentially('Second line', { delay: 50 });

  // Verify textarea contains both lines (newline character between them)
  const value = await chatInput.inputValue();
  expect(value).toContain('First line');
  expect(value).toContain('Second line');
  expect(value).toContain('\n');

  // URL should remain at / (message was not sent)
  await expect(page).toHaveURL(`${BASE_URL}/`);
});
