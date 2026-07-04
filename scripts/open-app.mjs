import { chromium } from '@playwright/test';

const browser = await chromium.launch({ headless: false, slowMo: 0 });
const page = await browser.newPage();

page.on('response', res => {
  if (res.status() >= 400) console.log('[HTTP]', res.status(), res.url());
});
page.on('pageerror', err => console.log('[PAGE ERROR]', err.message));

await page.goto('http://localhost:5000');
console.log('Browser open at:', page.url());

// Keep open until killed
await new Promise(() => {});
