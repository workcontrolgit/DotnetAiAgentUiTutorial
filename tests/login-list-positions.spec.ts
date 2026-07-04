import { test, expect } from '@playwright/test';

const EMAIL = 'fuji.nguyen@workcontrol.com';
const PASSWORD = 'gasoline87';
const PROMPT = 'list open positions';
const BASE_URL = 'http://localhost:5000';

// Existing session that has a previous assistant response.
// Using a pre-existing session means SendPromptAsync skips CreateSessionAsync + Nav.NavigateTo,
// so SessionsSidebar.OnLocationChanged never fires during the send — no concurrent EF Core race.
const EXISTING_SESSION_ID = '7cfc32b1-1112-4891-ac9b-79f38aa4165f';

test('login and list open positions', async ({ page }) => {
  test.setTimeout(150_000); // AI + MCP startup can take a while

  // 1. Log in
  await page.goto(`${BASE_URL}/`, { waitUntil: 'load' });
  await expect(page).toHaveURL(/\/login/);
  await page.fill('[placeholder="you@example.com"]', EMAIL);
  await page.fill('[placeholder="Password"]', PASSWORD);
  await page.click('button:has-text("Sign In")');
  await page.waitForURL(/\/(?!login)/, { timeout: 10_000 });
  console.log('Logged in.');

  // 2. Navigate directly to an existing workspace (auth cookie already set).
  //    This sets SessionId on the component so Send won't call CreateSessionAsync
  //    or Nav.NavigateTo — no LocationChanged fires, no concurrent DbContext race.
  await page.goto(`${BASE_URL}/workspace/${EXISTING_SESSION_ID}`, { waitUntil: 'load' });
  console.log('On workspace:', page.url());

  // 3. Wait for Send button to be enabled, then add extra settle time so all
  //    initialization DB ops (sidebar + workspace load) complete before Send.
  const sendBtn = page.locator('button.primary-btn:not([disabled])');
  await sendBtn.waitFor({ state: 'visible', timeout: 20_000 });
  await page.waitForTimeout(2_000);
  console.log('Page settled, send button enabled.');

  // 4. Type prompt — pressSequentially fires oninput per char → Blazor SignalR sync
  const chatInput = page.locator('textarea.chat-input');
  await chatInput.pressSequentially(PROMPT, { delay: 50 });
  await page.waitForTimeout(600);
  console.log('Prompt typed.');

  // 5. Click Send
  await sendBtn.click();
  console.log('Send clicked.');

  // 6. Wait for loading indicator (_busy=true → .chat-bubble--loading)
  const loading = page.locator('.chat-bubble--loading');
  await loading.waitFor({ state: 'visible', timeout: 20_000 });
  console.log('Loading indicator visible — agent is responding...');

  // 7. Wait for loading to disappear (AI + MCP complete)
  await loading.waitFor({ state: 'hidden', timeout: 120_000 });
  console.log('Response complete.');

  // 8. Assert assistant message and print it
  const assistantMsg = page.locator('.chat-bubble-row--assistant .chat-bubble:not(.chat-bubble--loading)').last();
  await expect(assistantMsg).toBeVisible();
  const text = await assistantMsg.innerText();
  console.log('\nAssistant response:\n', text.substring(0, 600));

  // 9. Screenshot
  await page.screenshot({ path: 'tests/result-list-positions.png', fullPage: false });
  console.log('Screenshot → tests/result-list-positions.png');
});
