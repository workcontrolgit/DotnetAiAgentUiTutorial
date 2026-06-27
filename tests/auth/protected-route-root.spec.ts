// spec: tests/hr-ai-agent.plan.md
// seed: tests/seed.spec.ts

import { test, expect } from '@playwright/test';
import { BASE_URL } from '../helpers';

test.describe('Authentication', () => {
  test('Unauthenticated access to root redirects to login with ReturnUrl', async ({ page }) => {
    // 1. Start in a fresh browser context (no auth cookies) and navigate to http://localhost:5000/
    await page.goto(`${BASE_URL}/`, { waitUntil: 'networkidle' });

    // expect: Page redirects to /login?ReturnUrl=%2F
    await expect(page).toHaveURL(`${BASE_URL}/login?ReturnUrl=%2F`);
    expect(page.url()).toContain('ReturnUrl=%2F');

    // expect: The Sign In form is displayed
    await expect(page.locator('h1, h2, h3').filter({ hasText: /sign in/i })).toBeVisible();
  });
});
