import { chromium } from 'playwright';

const EMAIL = 'fuji.nguyen@workcontrol.com';
const PASSWORD = 'gasoline87';
const BASE_URL = 'http://localhost:5000';
const PROMPT = 'list open positions';

const browser = await chromium.launch({ headless: false, slowMo: 300 });
const page = await browser.newPage();

page.on('console', msg => {
  if (msg.type() === 'error') console.log(`[CONSOLE ERROR] ${msg.text()}`);
});
page.on('response', resp => {
  if (resp.status() >= 400) console.log(`[HTTP ${resp.status()}] ${resp.url()}`);
});

console.log('--- Step 1: Navigate to app ---');
await page.goto(BASE_URL, { waitUntil: 'networkidle' });
console.log(`Current URL: ${page.url()}`);

// Login if redirected
if (page.url().includes('/login')) {
  console.log('--- Step 2: Logging in ---');
  await page.fill('input[name="email"], input[type="email"], #email', EMAIL);
  await page.fill('input[name="password"], input[type="password"], #password', PASSWORD);
  await page.click('button[type="submit"]');
  await page.waitForNavigation({ timeout: 10000 }).catch(() => {});
  console.log(`After login URL: ${page.url()}`);
}

// Wait for Blazor SignalR to connect AND auth to resolve (Send button enabled)
console.log('--- Step 3: Waiting for workspace to load ---');
await page.waitForSelector('.primary-btn:not([disabled])', { timeout: 20000 });
console.log('Send button enabled (auth resolved).');

// Send prompt
console.log(`--- Step 4: Sending prompt: "${PROMPT}" ---`);
await page.fill('.chat-input', PROMPT);
await page.click('.primary-btn:not([disabled])');

// Wait for response (up to 200 seconds — MCP + AI can be slow with local Ollama)
console.log('--- Step 5: Waiting for response (up to 200s) ---');

// Poll every 5s so we can see progress
let responded = false;
for (let i = 0; i < 40; i++) {
  await page.waitForTimeout(5000);
  const state = await page.evaluate(() => {
    const loading = document.querySelector('.chat-bubble--loading');
    const assistantBubbles = document.querySelectorAll('.chat-bubble-row--assistant .chat-bubble:not(.chat-bubble--loading)');
    return { loading: !!loading, count: assistantBubbles.length };
  });
  console.log(`  [${(i+1)*5}s] loading=${state.loading} assistantBubbles=${state.count}`);
  if (state.count > 0 && !state.loading) { responded = true; break; }
}

if (!responded) {
  console.log('WARNING: No response received within 200s');
}

// Read response text
const responseText = await page.evaluate(() => {
  const bubbles = document.querySelectorAll('.chat-bubble-row--assistant .chat-bubble');
  const real = Array.from(bubbles).find(b => !b.classList.contains('chat-bubble--loading'));
  return real ? real.innerText : '(no response found)';
});

console.log('\n--- Assistant Response ---');
console.log(responseText.substring(0, 2000));
console.log('--- End of Response ---');
console.log(`\nFinal URL: ${page.url()}`);

await page.screenshot({ path: 'scripts/result.png', fullPage: false });
console.log('Screenshot saved to scripts/result.png');

await browser.close();
