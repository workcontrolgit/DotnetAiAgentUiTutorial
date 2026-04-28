const fs = require('fs');

module.exports = async (page) => {
  const BATCH_SIZE = 30;
  const stories = JSON.parse(fs.readFileSync('c:/apps/DotnetMcpTutorial/medium-published-stories.json', 'utf8'));

  // Find starting index (first story without tags field or with empty tags that hasn't been processed)
  const startIdx = stories.findIndex(s => !s.hasOwnProperty('tags') || s.tags === undefined);
  const batchStart = startIdx === -1 ? 0 : startIdx;
  const batchEnd = Math.min(batchStart + BATCH_SIZE, stories.length);

  const extract = () => {
    const tags = [...new Set(Array.from(document.querySelectorAll('a[href*="/tag/"]')).map(a => a.innerText.trim()).filter(t => t))];
    let author = null;
    for (const a of document.querySelectorAll('a[href*="/@"]')) {
      const t = a.innerText.trim();
      if (t && t !== 'Profile' && !t.includes('follower') && !t.includes('following') && t.length > 1 && t.length < 60) { author = t; break; }
    }
    const h1 = document.querySelector('h1');
    let subtitle = null;
    if (h1) {
      let el = h1.nextElementSibling;
      for (let i = 0; i < 4 && el; i++) {
        const t = el.innerText?.trim();
        if (t && t.length > 5 && t.length < 300 && !t.includes('min read') && el.tagName !== 'FIGURE') { subtitle = t; break; }
        el = el.nextElementSibling;
      }
    }
    const m = document.body.innerText.match(/(\d+)\s+[Rr]esponse/);
    return { tags, author, subtitle, responseCount: m ? parseInt(m[1]) : 0 };
  };

  for (let i = batchStart; i < batchEnd; i++) {
    try {
      await page.goto(stories[i].url, { waitUntil: 'domcontentloaded', timeout: 15000 });
      await page.waitForTimeout(400);
      Object.assign(stories[i], await page.evaluate(extract));
    } catch(e) {
      stories[i].tags = stories[i].tags || [];
      stories[i].author = stories[i].author || null;
      stories[i].subtitle = stories[i].subtitle || null;
      stories[i].responseCount = 0;
      stories[i].error = e.message;
    }
  }

  fs.writeFileSync('c:/apps/DotnetMcpTutorial/medium-published-stories.json', JSON.stringify(stories, null, 2), 'utf8');

  const processed = stories.slice(batchStart, batchEnd);
  const withTags = processed.filter(s => s.tags?.length > 0).length;
  return {
    batchStart, batchEnd, total: stories.length,
    withTags,
    sample: processed.slice(0, 2).map(s => ({ title: s.title.slice(0, 40), tags: s.tags, author: s.author }))
  };
};
