import { test, expect } from '@playwright/test';
import { login, BASE_URL, EXISTING_SESSION_ID } from '../helpers';

test('delete a non-primary session removes it from the sidebar', async ({ page }) => {
  await login(page);
  await page.goto(`${BASE_URL}/`, { waitUntil: 'load' });

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

  // Find a session that is NOT the existing session ID (uses data-session-id attribute)
  let targetIndex = -1;
  for (let i = 0; i < sessionCount; i++) {
    const sessionItem = allSessions.nth(i);
    const sessionId = await sessionItem.evaluate((el: Element) => el.getAttribute('data-session-id'));
    if (sessionId !== EXISTING_SESSION_ID) {
      targetIndex = i;
      break;
    }
  }

  if (targetIndex === -1) {
    test.skip(true, 'No deletable session found that is not EXISTING_SESSION_ID');
    return;
  }

  const targetSession = allSessions.nth(targetIndex);
  const targetSessionId = await targetSession.evaluate((el: Element) => el.getAttribute('data-session-id'));

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

  // Verify the deleted session is no longer present (by ID, not name — names can be duplicates)
  const deletedItemStillPresent = await page.locator(`.session-item[data-session-id="${targetSessionId}"]`).count();
  expect(deletedItemStillPresent).toBe(0);
});
