/**
 * Extract per-story details (subtitle, tags, responseCount, author) for all 308 stories.
 * Uses saved Medium session cookies for authenticated access.
 * Run: node Playwright/specs/extract-story-details.js
 */

const { chromium } = require('@playwright/test');
const fs = require('fs');
const path = require('path');

const STORIES_FILE = path.join(__dirname, '../../medium-published-stories.json');
const AUTH_FILE = path.join(__dirname, '../../medium-auth-state.json');
const CONCURRENCY = 5;

function extractDetails() {
  const tags = [...new Set(
    Array.from(document.querySelectorAll('a[href*="/tag/"]'))
      .map(a => a.innerText.trim()).filter(t => t)
  )];

  let author = null;
  for (const a of document.querySelectorAll('a[href*="/@"]')) {
    const t = a.innerText.trim();
    if (t && t !== 'Profile' && !t.includes('follower') && !t.includes('following') && t.length > 1 && t.length < 60) {
      author = t; break;
    }
  }

  const h1 = document.querySelector('h1');
  let subtitle = null;
  if (h1) {
    let el = h1.nextElementSibling;
    for (let i = 0; i < 4 && el; i++) {
      const t = el.innerText?.trim();
      if (t && t.length > 5 && t.length < 300 && !t.includes('min read') && el.tagName !== 'FIGURE') {
        subtitle = t; break;
      }
      el = el.nextElementSibling;
    }
  }

  const m = document.body.innerText.match(/(\d+)\s+[Rr]esponse/);
  return { tags, author, subtitle, responseCount: m ? parseInt(m[1]) : 0 };
}

async function processBatch(browser, stories, indices) {
  const context = await browser.newContext({ storageState: AUTH_FILE });
  const page = await context.newPage();

  for (const i of indices) {
    process.stdout.write(`  [${i + 1}/${stories.length}] ${stories[i].title.slice(0, 55)}...\r`);
    try {
      await page.goto(stories[i].url, { waitUntil: 'domcontentloaded', timeout: 20000 });
      await page.waitForTimeout(400);
      Object.assign(stories[i], await page.evaluate(extractDetails));
    } catch (e) {
      stories[i].tags = stories[i].tags || [];
      stories[i].author = null;
      stories[i].subtitle = null;
      stories[i].responseCount = 0;
      stories[i].error = e.message;
    }
  }

  await context.close();
}

async function main() {
  const stories = JSON.parse(fs.readFileSync(STORIES_FILE, 'utf8'));
  console.log(`Extracting details for ${stories.length} stories (${CONCURRENCY} parallel workers)...`);

  const browser = await chromium.launch({ headless: true });

  // Split indices across workers
  const allIndices = stories.map((_, i) => i);
  const chunkSize = Math.ceil(allIndices.length / CONCURRENCY);
  const chunks = [];
  for (let i = 0; i < CONCURRENCY; i++) {
    const chunk = allIndices.slice(i * chunkSize, (i + 1) * chunkSize);
    if (chunk.length) chunks.push(chunk);
  }

  await Promise.all(chunks.map(indices => processBatch(browser, stories, indices)));
  await browser.close();

  fs.writeFileSync(STORIES_FILE, JSON.stringify(stories, null, 2), 'utf8');

  const withTags = stories.filter(s => s.tags?.length > 0).length;
  const withAuthor = stories.filter(s => s.author).length;
  const withSubtitle = stories.filter(s => s.subtitle).length;

  console.log(`\nDone!`);
  console.log(`  With tags:     ${withTags}/${stories.length}`);
  console.log(`  With author:   ${withAuthor}/${stories.length}`);
  console.log(`  With subtitle: ${withSubtitle}/${stories.length}`);
  console.log('\nSample:');
  console.log(JSON.stringify(stories[0], null, 2));
}

main().catch(console.error);
