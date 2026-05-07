/**
 * list-drafts.mjs
 *
 * Lists all draft articles from the Medium submissions outbox.
 *
 * Requires: medium-auth-state.json at the project root
 *
 * Usage:
 *   node medium/scripts/list-drafts.mjs
 *
 * Via Claude skill (no auth file needed):
 *   /medium-editor list-drafts
 */

import { chromium } from '@playwright/test';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = join(__dirname, '../..');
const AUTH_FILE = join(ROOT, 'medium-auth-state.json');

const EXTRACT_DRAFTS_JS = `(() => {
  const rows = Array.from(document.querySelectorAll('table tr')).slice(1);
  return rows.map(row => {
    const link = row.querySelector('a[href*="/p/"]');
    const href = link?.href || '';
    const editId = href.match(/\\/p\\/([a-f0-9]+)\\/edit/)?.[1] || null;
    const cells = row.innerText.split('\\t').map(s => s.trim()).filter(Boolean);
    const status = cells.find(c => /Pending review|Approved|Draft|Published/.test(c)) || '';
    return {
      title: link?.innerText?.trim() || cells[0] || '',
      status,
      editId,
      editUrl: href || null
    };
  }).filter(r => r.title);
})()`;

async function main() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ storageState: AUTH_FILE });
  const page = await context.newPage();

  console.log('Fetching drafts from Medium outbox...\n');

  await page.goto('https://medium.com/me/stories?tab=submissions-outbox', {
    waitUntil: 'networkidle',
    timeout: 30_000
  });

  const drafts = await page.evaluate(EXTRACT_DRAFTS_JS);

  console.log(`Found ${drafts.length} articles:\n`);
  console.table(drafts);

  await browser.close();
}

main().catch(err => { console.error(err); process.exit(1); });
