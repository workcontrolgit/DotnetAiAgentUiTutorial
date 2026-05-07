/**
 * get-draft-urls.mjs
 *
 * Batch-fetches "Share draft link" URLs for all articles in medium-public-url.json
 * and optionally writes the updated draftUrl values back to the file.
 *
 * Requires: medium-auth-state.json at the project root
 *
 * Usage:
 *   node medium/scripts/get-draft-urls.mjs           # print only
 *   node medium/scripts/get-draft-urls.mjs --save    # update medium-public-url.json
 *
 * For a single article via Claude skill:
 *   /medium-editor get-draft-url {editId}
 */

import { readFileSync, writeFileSync } from 'fs';
import { chromium } from '@playwright/test';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = join(__dirname, '../..');
const URL_MAP_FILE = join(ROOT, 'medium/medium-public-url.json');
const AUTH_FILE = join(ROOT, 'medium-auth-state.json');

async function getDraftUrl(page, editId) {
  await page.goto(`https://medium.com/p/${editId}/edit`, {
    waitUntil: 'domcontentloaded',
    timeout: 30_000
  });
  await page.waitForTimeout(1500);

  // Click the "..." more actions button
  await page.click('[data-action="show-post-actions-popover"]');
  await page.waitForTimeout(500);

  // Click "Share draft link"
  await page.click('text=Share draft link');
  await page.waitForTimeout(500);

  // Read the URL from the input
  const url = await page.evaluate(
    `document.querySelector('input[value*="medium.com"]')?.value`
  );

  // Dismiss — click elsewhere
  await page.keyboard.press('Escape');
  await page.waitForTimeout(300);

  return url || null;
}

async function main() {
  const shouldSave = process.argv.includes('--save');
  const urlMap = JSON.parse(readFileSync(URL_MAP_FILE, 'utf8'));

  const browser = await chromium.launch({ headless: false });
  const context = await browser.newContext({ storageState: AUTH_FILE });
  const page = await context.newPage();

  console.log(`Fetching draft URLs for ${urlMap.length} articles...\n`);

  const results = [];
  for (const entry of urlMap) {
    process.stdout.write(`  ${entry.part} (${entry.editId})... `);
    try {
      const draftUrl = await getDraftUrl(page, entry.editId);
      const changed = draftUrl && draftUrl !== entry.draftUrl;
      results.push({ part: entry.part, editId: entry.editId, draftUrl, changed });
      entry.draftUrl = draftUrl || entry.draftUrl;
      console.log(changed ? `UPDATED → ${draftUrl}` : 'unchanged');
    } catch (err) {
      results.push({ part: entry.part, editId: entry.editId, draftUrl: null, error: err.message });
      console.log(`ERROR: ${err.message}`);
    }
  }

  console.log('\nSummary:');
  console.table(results.map(r => ({ part: r.part, draftUrl: r.draftUrl?.slice(0, 80), changed: r.changed })));

  if (shouldSave) {
    writeFileSync(URL_MAP_FILE, JSON.stringify(urlMap, null, 2));
    console.log(`\nSaved to ${URL_MAP_FILE}`);
  } else {
    console.log('\nRun with --save to write changes to medium-public-url.json');
  }

  await browser.close();
}

main().catch(err => { console.error(err); process.exit(1); });
