/**
 * Extract per-story details (subtitle, tags, responseCount, author) for all 308 stories.
 * Reads medium-published-stories.json, visits each URL, updates with new fields.
 * Run: node Playwright/specs/extract-story-details.js
 */

const { chromium } = require('@playwright/test');
const fs = require('fs');
const path = require('path');

const STORIES_FILE = path.join(__dirname, '../../medium-published-stories.json');
const CONCURRENCY = 5;

async function extractStoryDetails(page, url) {
  try {
    await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 20000 });

    return await page.evaluate(() => {
      // Subtitle: element right after h1 that isn't an image/figure
      const h1 = document.querySelector('h1');
      let subtitle = null;
      if (h1) {
        let el = h1.nextElementSibling;
        for (let i = 0; i < 4 && el; i++) {
          const tag = el.tagName;
          const t = el.innerText?.trim();
          if (t && t.length > 5 && !t.includes('min read') && tag !== 'FIGURE' && tag !== 'IMG') {
            subtitle = t.length > 300 ? null : t; // skip if it's body content
            break;
          }
          el = el.nextElementSibling;
        }
      }

      // Tags
      const tagLinks = Array.from(document.querySelectorAll('a[href*="/tag/"]'));
      const tags = [...new Set(tagLinks.map(a => a.innerText.trim()).filter(t => t))];

      // Author
      const authorLinks = Array.from(document.querySelectorAll('a[href*="/@"]'));
      let author = null;
      for (const a of authorLinks) {
        const t = a.innerText.trim();
        if (t && t !== 'Profile' && !t.includes('follower') && !t.includes('following') && t.length > 1 && t.length < 60) {
          author = t;
          break;
        }
      }

      // Response count
      let responseCount = 0;
      const allText = document.body.innerText;
      const respMatch = allText.match(/(\d+)\s+[Rr]esponse/);
      if (respMatch) responseCount = parseInt(respMatch[1]);

      return { subtitle, tags, author, responseCount };
    });
  } catch (err) {
    return { subtitle: null, tags: [], author: null, responseCount: 0, error: err.message };
  }
}

async function runBatch(browser, stories, startIdx, endIdx) {
  const context = await browser.newContext();
  const page = await context.newPage();
  const results = [];

  for (let i = startIdx; i < endIdx; i++) {
    const story = stories[i];
    process.stdout.write(`[${i + 1}/${stories.length}] ${story.title.slice(0, 50)}...\r`);
    const details = await extractStoryDetails(page, story.url);
    results.push({ ...story, ...details });
  }

  await context.close();
  return results;
}

async function main() {
  const stories = JSON.parse(fs.readFileSync(STORIES_FILE, 'utf8'));
  console.log(`Processing ${stories.length} stories with concurrency ${CONCURRENCY}...`);

  const browser = await chromium.launch({ headless: true });

  const chunkSize = Math.ceil(stories.length / CONCURRENCY);
  const chunks = [];
  for (let i = 0; i < CONCURRENCY; i++) {
    const start = i * chunkSize;
    const end = Math.min(start + chunkSize, stories.length);
    if (start < stories.length) chunks.push({ start, end });
  }

  const allResults = await Promise.all(
    chunks.map(({ start, end }) => runBatch(browser, stories, start, end))
  );

  await browser.close();

  const merged = allResults.flat().sort((a, b) => {
    const idxA = stories.findIndex(s => s.id === a.id);
    const idxB = stories.findIndex(s => s.id === b.id);
    return idxA - idxB;
  });

  fs.writeFileSync(STORIES_FILE, JSON.stringify(merged, null, 2), 'utf8');
  console.log(`\nDone! Saved ${merged.length} stories to ${STORIES_FILE}`);
  console.log('Sample:', JSON.stringify(merged[0], null, 2));
}

main().catch(console.error);
