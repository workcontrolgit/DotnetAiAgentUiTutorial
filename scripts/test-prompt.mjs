import { chromium } from '@playwright/test';

const browser = await chromium.launch({ headless: false });
const page = await browser.newPage();

page.on('console', msg => { if (msg.type() === 'error') console.log('[ERR]', msg.text()); });
page.on('response', res => { if (res.status() >= 400) console.log('[HTTP]', res.status(), res.url()); });

// Register a test user
await page.goto('http://localhost:5000/register');
await page.waitForSelector('input[type="email"]');
await page.fill('input[type="email"]', 'test@test.com');
await page.fill('input[type="password"]', 'test123');
await page.click('button[type="submit"]');
await page.waitForURL('**/', { timeout: 8000 }).catch(() => {});
console.log('After register:', page.url());

await page.screenshot({ path: 'scripts/ss-1-after-register.png', fullPage: true });

// Send the prompt
const textarea = page.locator('textarea.chat-input');
if (await textarea.isVisible({ timeout: 5000 })) {
  await textarea.fill('list open positions');
  await page.keyboard.press('Enter');
  console.log('Prompt sent, waiting for response...');
  await page.waitForTimeout(30000);
  await page.screenshot({ path: 'scripts/ss-2-after-prompt.png', fullPage: true });
  const status = await page.locator('.panel-status').textContent().catch(() => '');
  const error = await page.locator('[class*="error"]').textContent().catch(() => '');
  console.log('Status text:', status);
  console.log('Error text:', error);
} else {
  await page.screenshot({ path: 'scripts/ss-1-no-input.png', fullPage: true });
  console.log('No chat input found');
}

await page.waitForTimeout(60000 * 3);
await browser.close();
