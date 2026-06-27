import { test, expect } from '@playwright/test';
import { login, BASE_URL, EXISTING_SESSION_ID } from '../helpers';

test('delete a non-primary session removes it from the sidebar', async ({ page }) => {
  await login(page);
  await page.goto(`${BASE_URL}/`, { waitUntil: 'networkidle' });

  // Wait for sessions to load
  await expect(page.locator('.session-item').first()).toBeVisible({ timeout: 10_000 });

  // Find all session items
  const allSessions = page.locator('.session-item');
  const sessionCount = await allSessions.count();

  // Skip if only one session exists (we must not delete EXISTING_SESSION_ID)
  if (sessionCount <= 1) {
    test.skip(true, 'Only one session exists — cannot delete without removing EXISTING_SESSION_ID');
    return;
  }

  // Find a session that is NOT the existing session ID
  let targetIndex = -1;
  for (let i = 0; i < sessionCount; i++) {
    const sessionItem = allSessions.nth(i);
    const html = await sessionItem.innerHTML();
    if (!html.includes(EXISTING_SESSION_ID)) {
      targetIndex = i;
      break;
    }
  }

  if (targetIndex === -1) {
    test.skip(true, 'No deletable session found that is not EXISTING_SESSION_ID');
    return;
  }

  const targetSession = allSessions.nth(targetIndex);
  const sessionNameText = await targetSession.locator('.session-name').textContent() ?? '';

  // Click the delete button on the target session
  await targetSession.locator('button.ghost-btn--icon[title="Delete"]').click();

  // Confirm deletion if a dialog appears
  page.on('dialog', async (dialog) => {
    await dialog.accept();
  });

  // Wait for the session to be removed
  await page.waitForTimeout(1_000);

  // Verify the session is no longer in the list
  const remainingSessions = page.locator('.session-item');
  const newCount = await remainingSessions.count();
  expect(newCount).toBe(sessionCount - 1);

  // Verify the deleted session name is no longer visible in the sidebar
  if (sessionNameText.trim()) {
    const sessionNames = await page.locator('.session-name').allTextContents();
    expect(sessionNames).not.toContain(sessionNameText.trim());
  }
});
