import { chromium } from '@playwright/test';
import fs from 'fs';

const browser = await chromium.launch({ headless: false });
const page = await browser.newPage();

// Capture console and page errors
page.on('console', msg => console.log('[BROWSER]', msg.type(), msg.text()));
page.on('pageerror', err => console.log('[PAGE ERROR]', err.message));
page.on('response', res => {
  if (res.status() >= 400) console.log('[HTTP]', res.status(), res.url());
});

console.log('Navigating to app...');
await page.goto('http://localhost:5000', { waitUntil: 'networkidle' });
console.log('Current URL:', page.url());
await page.screenshot({ path: 'scripts/screenshot-1-landing.png', fullPage: true });

// If redirected to login, fill credentials
if (page.url().includes('/login')) {
  console.log('Login page detected, filling credentials...');
  await page.fill('input[type="email"]', 'admin@example.com');
  await page.fill('input[type="password"]', 'Password1!');
  await page.click('button[type="submit"]');
  await page.waitForURL('**/', { timeout: 5000 }).catch(() => {});
  console.log('After login URL:', page.url());
  await page.screenshot({ path: 'scripts/screenshot-2-after-login.png', fullPage: true });
}

// Send the prompt
const textarea = page.locator('textarea.chat-input');
if (await textarea.isVisible()) {
  console.log('Sending prompt: list open position');
  await textarea.fill('list open position');
  await page.keyboard.press('Enter');
  await page.waitForTimeout(8000);
  await page.screenshot({ path: 'scripts/screenshot-3-after-prompt.png', fullPage: true });
  console.log('Screenshot saved.');
} else {
  console.log('Chat input not found.');
  await page.screenshot({ path: 'scripts/screenshot-3-no-input.png', fullPage: true });
}

console.log('Done. Browser staying open...');
await page.waitForTimeout(60000 * 5);
await browser.close();
