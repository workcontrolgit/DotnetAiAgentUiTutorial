/**
 * update-series-links.mjs
 *
 * Standalone script: reads medium-public-url.json, opens a Medium article in a
 * Playwright browser, and updates all series navigation links to their friendly URLs.
 *
 * Requires: medium-auth-state.json at the project root (export once from a live
 * MCP session via page.context().storageState({ path: 'medium-auth-state.json' }))
 *
 * Usage:
 *   node medium/scripts/update-series-links.mjs <editId>
 *   node medium/scripts/update-series-links.mjs f43127400757
 *
 * Via Claude skill (no auth file needed — uses live MCP browser session):
 *   /medium-editor update-series-links f43127400757
 */

import { readFileSync } from 'fs';
import { chromium } from '@playwright/test';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = join(__dirname, '../..');
const URL_MAP_FILE = join(ROOT, 'medium/medium-public-url.json');
const AUTH_FILE = join(ROOT, 'medium-auth-state.json');

// ── JS payloads (also used inline via /medium-editor skill) ──────────────────

/** Returns all <a> elements in the Medium editor with text, href, strongText */
export const READ_LINKS_JS = `(() => {
  const editor = document.querySelector('.postArticle-content');
  if (!editor) return { error: 'editor not found' };
  return Array.from(editor.querySelectorAll('a')).map(a => ({
    text: a.innerText.trim(),
    href: a.href,
    strongText: a.querySelector('strong')?.innerText?.trim() || null,
    emText: a.querySelector('em')?.innerText?.trim() || null
  }));
})()`;

/**
 * Updates an existing <a> element whose text includes matchText.
 * Falls back to searching <strong> elements not yet wrapped in <a>.
 */
export function buildUpdateLinkJS(matchText, newUrl) {
  return `(() => {
    const editor = document.querySelector('.postArticle-content');
    // Try existing <a> first
    let target = Array.from(editor.querySelectorAll('a'))
      .find(a => a.innerText.trim().includes(${JSON.stringify(matchText)}) ||
                 a.querySelector('strong')?.innerText?.trim().includes(${JSON.stringify(matchText)}));
    // Fall back to bare <strong> not inside an <a>
    if (!target) {
      target = Array.from(editor.querySelectorAll('strong'))
        .find(s => s.textContent.trim().includes(${JSON.stringify(matchText)}) && !s.closest('a'));
    }
    if (!target) return { error: 'not found: ' + ${JSON.stringify(matchText)} };
    target.click();
    const range = document.createRange();
    range.selectNodeContents(target);
    const sel = window.getSelection();
    sel.removeAllRanges();
    sel.addRange(range);
    target.closest('[contenteditable]').focus();
    const ok = document.execCommand('createLink', false, ${JSON.stringify(newUrl)});
    return { ok, text: target.innerText.trim(), newUrl: ${JSON.stringify(newUrl)} };
  })()`;
}

// ── Main ─────────────────────────────────────────────────────────────────────

async function main() {
  const editId = process.argv[2];
  if (!editId) {
    console.error('Usage: node update-series-links.mjs <editId>');
    process.exit(1);
  }

  const urlMap = JSON.parse(readFileSync(URL_MAP_FILE, 'utf8'));
  const editUrl = `https://medium.com/p/${editId}/edit`;

  console.log(`\nOpening: ${editUrl}`);
  console.log(`URL map: ${urlMap.length} entries\n`);

  const browser = await chromium.launch({ headless: false });
  const context = await browser.newContext({ storageState: AUTH_FILE });
  const page = await context.newPage();

  await page.goto(editUrl, { waitUntil: 'domcontentloaded', timeout: 30_000 });
  await page.waitForTimeout(2000);

  // Read current links
  const links = await page.evaluate(READ_LINKS_JS);
  if (links.error) { console.error('Error:', links.error); await browser.close(); process.exit(1); }

  console.log('Current links:');
  console.table(links.map(l => ({ text: l.text.slice(0, 50), href: l.href.slice(0, 80) })));

  // Match links to URL map entries
  const results = [];
  for (const entry of urlMap) {
    const partNum = entry.part?.replace('part-', '') || '';
    const matchTerms = [
      entry.title,
      `Part ${partNum}:`,
      `Part ${partNum} —`,
      `Read Part ${partNum}`,
      `→ Read Part ${partNum}`,
    ].filter(Boolean);

    const matchingLink = links.find(l =>
      matchTerms.some(term =>
        l.text?.includes(term) || l.strongText?.includes(term) || l.emText?.includes(term)
      )
    );

    if (!matchingLink) continue;
    if (matchingLink.href === entry.draftUrl) {
      results.push({ part: entry.part, status: 'already correct', url: entry.draftUrl });
      continue;
    }

    const result = await page.evaluate(buildUpdateLinkJS(matchingLink.text || matchingLink.strongText, entry.draftUrl));
    results.push({ part: entry.part, status: result.ok ? 'updated' : 'failed', url: entry.draftUrl, error: result.error });
    await page.waitForTimeout(300);
  }

  // Trigger auto-save
  await page.keyboard.press('End');
  await page.waitForTimeout(2000);

  console.log('\nResults:');
  console.table(results);
  console.log('\nDone — Medium auto-save triggered.');

  await browser.close();
}

main().catch(err => { console.error(err); process.exit(1); });
