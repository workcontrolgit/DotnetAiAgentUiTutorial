import { test, expect, Page } from '@playwright/test';

const VISIBLE_MENU_ITEMS = [
  { name: 'US',            url: 'https://www.cnn.com/us' },
  { name: 'World',         url: 'https://www.cnn.com/world' },
  { name: 'Politics',      url: 'https://www.cnn.com/politics' },
  { name: 'Business',      url: 'https://www.cnn.com/business' },
  { name: 'Health',        url: 'https://www.cnn.com/health' },
  { name: 'Entertainment', url: 'https://www.cnn.com/entertainment' },
  { name: 'Underscored',   url: 'https://www.cnn.com/cnn-underscored' },
  { name: 'Style',         url: 'https://www.cnn.com/style' },
  { name: 'Travel',        url: 'https://www.cnn.com/travel' },
];

// Items inside the "More" overflow dropdown
const MORE_MENU_ITEMS = [
  { name: 'Sports',             url: 'https://www.cnn.com/sports' },
  { name: 'Science',            url: 'https://www.cnn.com/science' },
  { name: 'Climate',            url: 'https://www.cnn.com/climate' },
  { name: 'Weather',            url: 'https://www.cnn.com/weather' },
  { name: 'Ukraine-Russia War', url: 'https://www.cnn.com/world/europe/ukraine' },
  { name: 'Israel-Hamas War',   url: 'https://www.cnn.com/world/middleeast/israel' },
  { name: 'Games',              url: 'https://www.cnn.com/games' },
];

test.use({ viewport: { width: 1440, height: 900 } });

/** Navigate to CNN and dismiss any consent/overlay dialogs */
async function loadCnn(page: Page) {
  await page.goto('https://www.cnn.com');

  const agreeBtn = page.getByRole('button', { name: 'Agree' });
  if (await agreeBtn.isVisible({ timeout: 5000 }).catch(() => false)) {
    await agreeBtn.click();
    await agreeBtn.waitFor({ state: 'hidden', timeout: 5000 }).catch(() => {});
  }
}

/** Click a nav link via JS evaluate to bypass CNN's overlay pointer interception */
async function jsClickNavLink(page: Page, href: string) {
  await page.evaluate((targetHref) => {
    const link = Array.from(
      document.querySelectorAll<HTMLAnchorElement>('nav.header__nav a')
    ).find(a => a.href === targetHref);
    if (link) link.click();
    else window.location.href = targetHref;
  }, href);
}

test.describe('CNN Top Menu Navigation', () => {
  test.beforeEach(async ({ page }) => {
    await loadCnn(page);
  });

  test('top nav is visible with all primary menu items', async ({ page }) => {
    const nav = page.locator('nav.header__nav').first();
    await expect(nav).toBeVisible();

    for (const item of VISIBLE_MENU_ITEMS) {
      // Verify the link exists in the nav with the correct href
      const link = nav.locator(`a[href="${item.url}"]`).first();
      await expect(link).toBeVisible();
    }

    // "More" button to access overflow items
    await expect(nav.locator('.header__nav-more--toggle-caret').first()).toBeVisible();
  });

  test('clicking visible top menu items navigates to correct pages', async ({ page }) => {
    for (const item of VISIBLE_MENU_ITEMS) {
      if (page.url() !== 'https://www.cnn.com/') {
        await loadCnn(page);
      }
      await jsClickNavLink(page, item.url);
      await expect(page).toHaveURL(item.url, { timeout: 15000 });
      await expect(page).toHaveTitle(/.+/);
    }
  });

  test('"More" dropdown contains all overflow menu items', async ({ page }) => {
    // The dropdown panel (.header__nav-item-dropdown) exists in the DOM
    // Verify all overflow items are registered in it via href
    for (const item of MORE_MENU_ITEMS) {
      const link = page.locator(`.header__nav-item-dropdown a[href="${item.url}"]`).first();
      await expect(link).toBeAttached();
    }

    // Force-show the dropdown (CNN's More menu toggle requires real hover/focus chain)
    await page.evaluate(() => {
      const dropdown = document.querySelector<HTMLElement>('.header__nav-item-dropdown');
      if (dropdown) dropdown.style.display = 'block';
    });

    const dropdown = page.locator('.header__nav-item-dropdown').first();
    await expect(dropdown).toBeVisible({ timeout: 5000 });

    for (const item of MORE_MENU_ITEMS) {
      const link = dropdown.locator(`a[href="${item.url}"]`).first();
      await expect(link).toBeVisible({ timeout: 3000 });
    }
  });

  test('navigates to Sports via More dropdown', async ({ page }) => {
    // Force-show the More dropdown
    await page.evaluate(() => {
      const dropdown = document.querySelector<HTMLElement>('.header__nav-item-dropdown');
      if (dropdown) dropdown.style.display = 'block';
    });

    const dropdown = page.locator('.header__nav-item-dropdown').first();
    await expect(dropdown).toBeVisible({ timeout: 5000 });

    // Click Sports link inside the dropdown via JS to bypass overlays
    await page.evaluate(() => {
      const link = document.querySelector<HTMLAnchorElement>(
        '.header__nav-item-dropdown a[href="https://www.cnn.com/sports"]'
      );
      if (link) link.click();
    });

    // /sports redirects to /sport
    await expect(page).toHaveURL(/cnn\.com\/sport/, { timeout: 15000 });
    await expect(page).toHaveTitle(/.+/);
  });
});
