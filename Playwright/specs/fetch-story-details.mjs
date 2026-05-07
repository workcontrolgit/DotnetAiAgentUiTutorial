/**
 * Fetches per-story details using Medium session cookies.
 * Node 18+ native fetch. Run: node Playwright/specs/fetch-story-details.mjs
 */

import { readFileSync, writeFileSync } from 'fs';
import { JSDOM } from 'jsdom';

const STORIES_FILE = 'c:/apps/DotnetMcpTutorial/medium-published-stories.json';
const CONCURRENCY = 8;

// Medium session cookies from the authenticated browser session
const COOKIE = [
  'sid=1:MKrGZdk2QMPHcLt6bSwBiFzPkb/F6EmALgFVV+grH3S2kHZNfH7tavW977j4ZZzx',
  'uid=4b79cc017e83',
  'xsrf=Q7KsvMyHYrvfwH7E',
  'cf_clearance=C5RIiLUPb4mjuAurhUGlELFylAzEBuxnNL5iqBzDKUM-1777406078-1.2.1.1-NYuYYtWRYE9xkc8qKmdUCFx_oetJstqK9PapzwDBqN.09WeLlWh6CQm3p4AKjOK26lMDF7aM1bkjN9EYEK0zRG09JtQP_z3pLuuxzO2LebfiynyqifBBhHurtGgeIK5x6F5TsaZUGhoscXGNVD8Dbouue7Bb9VkU0ZK7kHoig2JwBmgku96wCdZVif0GS4JiD1umjFMTuG2_AHS8.yIY8wk8FsNJD5R5azr.NHD6tm7J9yWw6c7Zwl1sRn5LnTAyNekqjYuHds.L9ihUedyFQ9eV2Py_lrbIn.KhKVlWd6Y_0.rihwDboubJfe2Ui9RDimwc2KVnSJRsLEeHu9JyjA',
].join('; ');

const HEADERS = {
  'Cookie': COOKIE,
  'Accept': 'text/html,application/xhtml+xml',
  'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36',
  'Accept-Language': 'en-US,en;q=0.9',
};

async function fetchStoryDetails(story) {
  try {
    const resp = await fetch(story.url, { headers: HEADERS });
    if (!resp.ok) return { ...story, fetchError: resp.status };

    const html = await resp.text();
    const dom = new JSDOM(html);
    const doc = dom.window.document;

    // Tags
    const tags = [...new Set(
      Array.from(doc.querySelectorAll('a[href*="/tag/"]'))
        .map(a => a.textContent.trim()).filter(t => t)
    )];

    // Author
    let author = null;
    for (const a of doc.querySelectorAll('a[href*="/@"]')) {
      const t = a.textContent.trim();
      if (t && t !== 'Profile' && !t.includes('follower') && !t.includes('following') && t.length > 1 && t.length < 60) {
        author = t; break;
      }
    }

    // Subtitle (element after h1)
    const h1 = doc.querySelector('h1');
    let subtitle = null;
    if (h1) {
      let el = h1.nextElementSibling;
      for (let i = 0; i < 4 && el; i++) {
        const t = el.textContent?.trim();
        if (t && t.length > 5 && t.length < 300 && !t.includes('min read') && el.tagName !== 'FIGURE') {
          subtitle = t; break;
        }
        el = el.nextElementSibling;
      }
    }

    // Response count
    const bodyText = doc.body?.textContent || '';
    const m = bodyText.match(/(\d+)\s+[Rr]esponse/);
    const responseCount = m ? parseInt(m[1]) : 0;

    return { ...story, tags, author, subtitle, responseCount };
  } catch (e) {
    return { ...story, tags: [], author: null, subtitle: null, responseCount: 0, fetchError: e.message };
  }
}

async function processBatch(stories) {
  return Promise.all(stories.map(fetchStoryDetails));
}

async function main() {
  const stories = JSON.parse(readFileSync(STORIES_FILE, 'utf8'));
  console.log(`Processing ${stories.length} stories (concurrency: ${CONCURRENCY})...`);

  const results = [];
  for (let i = 0; i < stories.length; i += CONCURRENCY) {
    const batch = stories.slice(i, i + CONCURRENCY);
    const batchResults = await processBatch(batch);
    results.push(...batchResults);
    process.stdout.write(`  ${Math.min(i + CONCURRENCY, stories.length)}/${stories.length}\r`);
  }

  writeFileSync(STORIES_FILE, JSON.stringify(results, null, 2), 'utf8');

  const withTags = results.filter(s => s.tags?.length > 0).length;
  const withAuthor = results.filter(s => s.author).length;
  const withSubtitle = results.filter(s => s.subtitle).length;

  console.log(`\nDone!`);
  console.log(`  With tags:     ${withTags}/${results.length}`);
  console.log(`  With author:   ${withAuthor}/${results.length}`);
  console.log(`  With subtitle: ${withSubtitle}/${results.length}`);
  console.log('\nSample:', JSON.stringify(results[0], null, 2));
}

main().catch(console.error);
