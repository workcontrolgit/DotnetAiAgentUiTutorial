# medium-editor

A skill for managing Medium.com articles using the MCP Playwright browser (`mcp__playwright__*` tools).

## Invocation

`/medium-editor <operation> [editId]`

Examples:
- `/medium-editor list-drafts`
- `/medium-editor read-links 64314313e3e7`
- `/medium-editor update-series-links f43127400757`
- `/medium-editor get-draft-url 81964133d3b8`
- `/medium-editor publish-article fc985ed7cbf5`

## Reference Data

Always read this file first for series URL mappings:
`c:/apps/DotnetMcpTutorial/medium/medium-public-url.json`

Each entry has: `part`, `title`, `editId`, `editUrl`, `draftUrl`

Edit URL pattern: `https://medium.com/p/{editId}/edit`

## Auth

The MCP Playwright browser maintains a persistent authenticated Medium session. No login steps are needed. If redirected to the Medium login page, the session has expired — ask the user to log in manually in the browser window, then retry.

## Key DOM Facts

- **Editor selector**: `.postArticle-content` (preferred) or `[contenteditable="true"]`
- **Link update method**: `document.execCommand('createLink', false, url)` — reliable and proven
- **Paragraph identity**: each `<p>` in the editor has a `name="XXXX"` attribute — use `[name="XXXX"]` to target specific paragraphs
- **Plain text nodes**: "Part 1", "Part 2", series header text live in raw text nodes, not `<strong>` — use TreeWalker to find them
- **`range.toString()` returns empty after execCommand** — this is normal. `ok: true` is the real success signal. Always verify by re-reading links after.
- **Series header boundary**: the text node for "AI Agents & MCP with .NET 10 | " includes the " | " separator — use `text.indexOf(' | ')` as the range end to avoid linking the pipe
- **Draft share URL input**: `input[value*="medium.com"]` — appears after clicking "Share draft link"
- **Save**: Medium auto-saves. After link updates, press End key or wait 2s for auto-save.

---

## Operations

### list-drafts

Navigate to the submissions outbox and extract all draft articles.

```js
// Navigate
await mcp__playwright__browser_navigate({ url: 'https://medium.com/me/stories?tab=submissions-outbox' });

// Extract table rows
await mcp__playwright__browser_evaluate({ function: () => {
  const rows = Array.from(document.querySelectorAll('table tr')).slice(1);
  return rows.map(row => {
    const link = row.querySelector('a[href*="/p/"]');
    const href = link?.href || '';
    const editId = href.match(/\/p\/([a-f0-9]+)\/edit/)?.[1] || null;
    const status = row.innerText.match(/Pending review|Approved|Draft/)?.[0] || '';
    return { title: link?.innerText?.trim(), status, editId, editUrl: href };
  }).filter(r => r.title);
}});
```

Return the list as a table to the user.

---

### open-article {editId}

```js
await mcp__playwright__browser_navigate({ url: `https://medium.com/p/${editId}/edit` });
```

Wait for `.postArticle-content` to be present. Confirm title matches expected article.

---

### read-links {editId}

Open the article, then read all links:

```js
await mcp__playwright__browser_evaluate({ function: () => {
  const editor = document.querySelector('.postArticle-content');
  return Array.from(editor.querySelectorAll('a')).map(a => ({
    text: a.innerText.trim(),
    href: a.href,
    strongText: a.querySelector('strong')?.innerText?.trim() || null,
    emText: a.querySelector('em')?.innerText?.trim() || null
  }));
}});
```

Return results to the user. Flag any links pointing to raw GitHub markdown files or relative paths — those need updating.

---

### update-series-links {editId}

Updates all series navigation links in an article to use friendly Medium URLs.

**Step 1** — Read `c:/apps/DotnetMcpTutorial/medium/medium-public-url.json` using the Read tool. Build a map of `{ matchText → draftUrl }`:
- Match on: part title (e.g. "Part 1: Clean Architecture"), part label (e.g. "Part 1"), series name
- Target URL: the `draftUrl` field for the matching entry

**Step 2** — Navigate to the article:
```js
await mcp__playwright__browser_navigate({ url: `https://medium.com/p/${editId}/edit` });
```

**Step 3** — Read all current series links and identify missing ones:
```js
await mcp__playwright__browser_evaluate({ function: () => {
  const editor = document.querySelector('.postArticle-content');
  return Array.from(editor.querySelectorAll('a'))
    .filter(a => a.href.includes('scrum-and-coke') || a.href.includes('medium.com/p/'))
    .map(a => ({ text: a.innerText.trim(), href: a.href }));
}});
```

Compare against the expected links from `medium-public-url.json`. For each missing link, find the paragraph it lives in (Step 3b) and add it (Step 4).

**Step 3b** — Discover paragraph `name` attributes for paragraphs that need linking:
```js
await mcp__playwright__browser_evaluate({ function: () => {
  const editor = document.querySelector('.postArticle-content');
  return Array.from(editor.querySelectorAll('p'))
    .filter(p => {
      const t = p.innerText;
      // Adjust search terms to match the article's content
      return t.includes('Part 1') || t.includes('Part 2') || t.includes('Part 3') ||
             t.includes('Part 4') || t.includes('AI Agents');
    })
    .map(p => ({ name: p.getAttribute('name'), text: p.innerText.trim().substring(0, 100) }));
}});
```

Note the `name` attribute for each paragraph — use it in Step 4 to target precisely.

**Step 4a** — Link plain text (most common: "Part N", series header) using TreeWalker:
```js
await mcp__playwright__browser_evaluate({ function: () => {
  const editor = document.querySelector('.postArticle-content');
  // Target the specific paragraph by its name attribute
  const para = editor.querySelector('[name="PARA_NAME"]');
  if (!para) return { error: 'para not found' };

  const walker = document.createTreeWalker(para, 4); // 4 = NodeFilter.SHOW_TEXT (constant unavailable in Playwright evaluate)
  let node;
  while ((node = walker.nextNode())) {
    if (node.textContent.includes('MATCH_TEXT')) {
      const text = node.textContent;
      const start = text.indexOf('MATCH_TEXT');
      // For series header: stop before " | " separator to avoid linking the pipe
      const pipeIdx = text.indexOf(' | ');
      const end = start + 'MATCH_TEXT'.length; // or pipeIdx if trimming a header
      const range = document.createRange();
      range.setStart(node, start);
      range.setEnd(node, end);
      const sel = window.getSelection();
      sel.removeAllRanges();
      sel.addRange(range);
      para.closest('[contenteditable]').focus();
      const ok = document.execCommand('createLink', false, 'NEW_URL');
      return { ok }; // range.toString() returns empty after execCommand — ok:true means success
    }
  }
  return { error: 'text node not found' };
}});
```

> **Note:** `range.toString()` always returns `""` after `execCommand` — this is normal. Verify success in Step 5.

**Step 4b** — Update an existing `<a>` with the wrong URL:
```js
await mcp__playwright__browser_evaluate({ function: () => {
  const editor = document.querySelector('.postArticle-content');
  const link = Array.from(editor.querySelectorAll('a'))
    .find(a => a.innerText.trim().includes('MATCH_TEXT'));
  if (!link) return { error: 'not found' };
  const range = document.createRange();
  range.selectNodeContents(link);
  const sel = window.getSelection();
  sel.removeAllRanges();
  sel.addRange(range);
  link.closest('[contenteditable]').focus();
  const ok = document.execCommand('createLink', false, 'NEW_URL');
  return { ok, text: link.innerText.trim() };
}});
```

**Step 4c** — Link a `<strong>` element (bold text, no hyperlink yet):
```js
await mcp__playwright__browser_evaluate({ function: () => {
  const editor = document.querySelector('.postArticle-content');
  const strong = Array.from(editor.querySelectorAll('strong'))
    .find(s => s.textContent.trim().includes('MATCH_TEXT') && !s.closest('a'));
  if (!strong) return { error: 'not found' };
  const range = document.createRange();
  range.selectNodeContents(strong);
  const sel = window.getSelection();
  sel.removeAllRanges();
  sel.addRange(range);
  strong.closest('[contenteditable]').focus();
  const ok = document.execCommand('createLink', false, 'NEW_URL');
  return { ok, text: strong.innerText.trim() };
}});
```

**Step 5** — Verify all series links are correct, then trigger auto-save:
```js
// Verify
await mcp__playwright__browser_evaluate({ function: () => {
  const editor = document.querySelector('.postArticle-content');
  return Array.from(editor.querySelectorAll('a'))
    .filter(a => a.href.includes('scrum-and-coke') || a.href.includes('medium.com/p/'))
    .map(a => ({ text: a.innerText.trim(), href: a.href }));
}});

// Auto-save
await mcp__playwright__browser_press_key({ key: 'End' });
```

Report a summary table: link text | URL | status (added / updated / already correct).

---

### get-draft-url {editId}

Retrieves the "Share draft link" (friendly URL) for an article.

```js
// Navigate to edit page
await mcp__playwright__browser_navigate({ url: `https://medium.com/p/${editId}/edit` });

// Click the "..." more actions button
await mcp__playwright__browser_click({ target: '[data-action="show-post-actions-popover"]' });

// Click "Share draft link"
await mcp__playwright__browser_click({ target: 'text=Share draft link' });

// Read the URL from the input
await mcp__playwright__browser_evaluate({ function: () =>
  document.querySelector('input[value*="medium.com"]')?.value
});
```

Return the draft URL to the user. Optionally offer to update `medium/medium-public-url.json` with the new URL if it differs.

---

### create-new-article {markdownFilePath}

Creates a brand new Medium draft from a local markdown file and populates the full body content.

**Step 1 — Navigate to new story:**
```js
await mcp__playwright__browser_navigate({ url: 'https://medium.com/new-story' });
```

**Step 2 — Type the title** (use `browser_evaluate` with `insertText`, NOT `browser_type` — `fill()` drops spaces around apostrophes and special chars):
```js
await mcp__playwright__browser_click({ target: 'h3:has-text("Title")', element: 'Title field' });
// Wait for URL to change to https://medium.com/p/{editId}/edit — note the new editId
await mcp__playwright__browser_evaluate({ function: () => {
  const title = document.querySelector('h3');
  title.focus();
  const range = document.createRange();
  range.selectNodeContents(title);
  const sel = window.getSelection();
  sel.removeAllRanges();
  sel.addRange(range);
  document.execCommand('delete');
  document.execCommand('insertText', false, 'YOUR TITLE HERE');
  return title.innerText;
}});
```

**Step 3 — Build HTML from the markdown file**, converting:
- `## Heading` → `<h2>Heading</h2>`
- `**text**` → `<strong>text</strong>`
- `` `code` `` → `<code>code</code>`
- `[text](url)` → `<a href="url">text</a>`
- `*text*` → `<em>text</em>`
- Paragraphs → `<p>...</p>`
- `---` dividers → omit (Medium doesn't use them)

**Step 4 — Copy HTML to clipboard via a temp element** (this is the ONLY reliable injection method — do NOT use `execCommand('insertHTML')` directly as it bypasses Medium's React state and causes a persistent save error):
```js
await mcp__playwright__browser_evaluate({ function: () => {
  const html = `...full HTML string...`;
  const tmp = document.createElement('div');
  tmp.setAttribute('contenteditable', 'true');
  tmp.style.cssText = 'position:fixed;top:0;left:0;opacity:0';
  tmp.innerHTML = html;
  document.body.appendChild(tmp);
  tmp.focus();
  const range = document.createRange();
  range.selectNodeContents(tmp);
  const sel = window.getSelection();
  sel.removeAllRanges();
  sel.addRange(range);
  document.execCommand('copy');
  document.body.removeChild(tmp);
  return true;
}});
```

**Step 5 — Click the body area and paste:**
```js
await mcp__playwright__browser_click({ target: 'p:has-text("Tell your story")', element: 'Body editor' });
await mcp__playwright__browser_press_key({ key: 'Control+v' });
await mcp__playwright__browser_wait_for({ time: 4 });
```

**Step 6 — Verify save:** Check the top bar shows "Draft · Saved" (not the red "Something is wrong" banner). If the red banner appears, the content was NOT saved via React state — reload the page, the body will be empty, and repeat from Step 4.

**Step 7 — Add topics:** Before publishing, add up to 5 topics in the publish dialog for discoverability.

**Step 8 — Note the editId** from the URL (`https://medium.com/p/{editId}/edit`) and add it to `medium/medium-public-url.json` after publishing.

---

### publish-article {editId}

Walks through the full Medium publish flow, including topics and publication submission.

**Step 1 — Open publish dialog:**
```js
// Navigate to edit page
await mcp__playwright__browser_navigate({ url: `https://medium.com/p/${editId}/edit` });

// Click Publish — button selector is unreliable, use evaluate instead
await mcp__playwright__browser_evaluate({ function: () => {
  const btn = Array.from(document.querySelectorAll('button')).find(b => b.textContent.trim() === 'Publish');
  if (btn) btn.click();
  return btn ? 'clicked' : 'not found';
}});
```

URL will change to `/submission?...&submitType=publishing-post`. Take snapshot to confirm dialog.

**Step 2 — Add topics (up to 5):**

Topics input starts as `placeholder="Add a topic..."`, then becomes `placeholder="Add more topics..."` after first tag is added.

- **Always use `execCommand('insertText')` to type into topic input** — `browser_type` may not trigger React handlers
- Medium tags only support letters, numbers, spaces, and dashes — `.NET` is invalid; use `dotnet` instead
- After typing, click the suggestion button using `evaluate` (ref-based clicks are unreliable in dropdowns)

```js
// Type into topic input
await mcp__playwright__browser_evaluate({ function: () => {
  const input = document.querySelector('input[placeholder="Add a topic..."]')
    || document.querySelector('input[placeholder="Add more topics..."]');
  if (input) { input.focus(); document.execCommand('insertText', false, 'TOPIC_NAME'); return 'typed'; }
  return 'not found';
}});

// Wait 1s for suggestions to appear, then click the best match
await mcp__playwright__browser_wait_for({ time: 1 });
await mcp__playwright__browser_evaluate({ function: () => {
  const btn = Array.from(document.querySelectorAll('button'))
    .find(b => b.textContent.includes('TOPIC_NAME (') && !b.textContent.includes('Remove'));
  if (btn) { btn.click(); return 'clicked: ' + btn.textContent.trim(); }
  return 'not found';
}});
```

Repeat for each topic. Recommended tags for .NET/MCP articles:
`Artificial Intelligence`, `Dotnet`, `Programming`, `Software Engineering`, `Technology`

**Step 3 — Submit to a publication:**

Click the **Submit** button (next to "your story to connect with community"):

```js
await mcp__playwright__browser_evaluate({ function: () => {
  const btn = Array.from(document.querySelectorAll('button')).find(b => b.textContent.trim() === 'Submit');
  if (btn) btn.click();
  return btn ? 'clicked' : 'not found';
}});
```

URL changes to `submitType=publication-submission`. A list of publications you contribute to appears.

**Step 4 — Select the publication:**
```js
await mcp__playwright__browser_evaluate({ function: () => {
  const btn = Array.from(document.querySelectorAll('button'))
    .find(b => b.textContent.includes('PUBLICATION_NAME'));
  if (btn) { btn.click(); return 'clicked'; }
  return 'not found';
}});
```

**Step 5 — Confirm and submit:**

After selecting a publication, two options appear:
- **"Send for review"** — submits to publication's review queue (editors must approve before publishing)
- **"Approve and publish"** — publishes immediately (only available if you are an editor of the publication)

Ask the user which they prefer, then click:
```js
await mcp__playwright__browser_evaluate({ function: () => {
  // Use 'Send for review' OR 'Approve and publish'
  const btn = Array.from(document.querySelectorAll('button'))
    .find(b => b.textContent.trim() === 'Send for review');
  if (btn) btn.click();
  return btn ? 'submitted' : 'not found';
}});
```

Success: page redirects to `https://medium.com/me/stories?tab=submissions-outbox`.

After publication, retrieve the public URL and update `medium/medium-public-url.json` with the new entry.

---

### insert-image {editId} {localFilePath} {anchorText}

Inserts a local image file after a specific paragraph in the article.

> **Critical gotchas:**
> - Do NOT use `browser_click` on the image button — it times out because the button is only visible while the editor has focus on an empty line.
> - Do NOT use `evaluate` to click the `+` button first pass without first setting cursor position via the Selection API — the image lands in the wrong section.
> - The correct sequence: **set cursor via Selection API → `End` → `Enter` → `evaluate` click `[data-action="inline-menu"]` → `evaluate` click `[data-action="inline-menu-image"]` → `browser_file_upload`**.

**Step 1 — Navigate to article:**
```js
await mcp__playwright__browser_navigate({ url: `https://medium.com/p/${editId}/edit` });
```

**Step 2 — Set cursor at end of the target paragraph using Selection API:**
```js
await mcp__playwright__browser_evaluate({ function: () => {
  const ANCHOR_TEXT = 'unique text from the paragraph after which to insert image';
  const editor = document.querySelector('[contenteditable="true"]');
  const walker = document.createTreeWalker(editor, NodeFilter.SHOW_TEXT);
  let node;
  while (node = walker.nextNode()) {
    if (node.textContent.includes(ANCHOR_TEXT)) {
      const range = document.createRange();
      range.setStart(node, node.textContent.length);
      range.setEnd(node, node.textContent.length);
      const sel = window.getSelection();
      sel.removeAllRanges();
      sel.addRange(range);
      editor.focus();
      return 'cursor set';
    }
  }
  return 'anchor text not found';
}});
```

**Step 3 — Create a blank line:**
```js
await mcp__playwright__browser_press_key({ key: 'End' });
await mcp__playwright__browser_press_key({ key: 'Enter' });
```

**Step 4 — Open the inline menu then click image upload (both via evaluate — `browser_click` times out on these):**
```js
// Open the + menu
await mcp__playwright__browser_evaluate({ function: () => {
  const btn = document.querySelector('[data-action="inline-menu"]');
  if (btn) { btn.click(); return 'clicked'; }
  return 'not found';
}});

// Click the image option
await mcp__playwright__browser_evaluate({ function: () => {
  const btn = document.querySelector('[data-action="inline-menu-image"]');
  if (btn) { btn.click(); return 'clicked'; }
  return 'not found';
}});
// Page will show: [File chooser]: can be handled by browser_file_upload
```

**Step 5 — Upload the file:**
```js
await mcp__playwright__browser_file_upload({ paths: ['C:\\absolute\\path\\to\\image.png'] });
```

**Step 6 — Verify** by taking a snapshot and confirming a `figure` element appears directly after the target paragraph and before the next heading.

**Notes:**
- `data-action="inline-menu"` is the `+` circle button; `data-action="inline-menu-image"` is the camera icon in the expanded menu.
- Medium auto-saves after image upload. No extra save step needed.
- To add a caption, click the `generic "Type caption for image (optional)"` element and type.

---

### replace-text {editId}

Replaces a specific word or phrase anywhere in the article body without disturbing surrounding content or formatting.

> **Critical:** Do NOT use `browser_type` (calls `fill()` and wipes the whole paragraph). Do NOT use `window.find()` — it returns `false` inside the Medium editor. The only reliable method is TreeWalker + Range + `execCommand('insertText')`.

**Step 1 — Navigate to article:**
```js
await mcp__playwright__browser_navigate({ url: `https://medium.com/p/${editId}/edit` });
```

**Step 2 — Find, select, and replace the target text:**
```js
await mcp__playwright__browser_evaluate({ function: () => {
  const FIND = 'exact text to find';
  const REPLACE = 'replacement text';

  const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
  let node;
  while (node = walker.nextNode()) {
    if (node.textContent.includes(FIND)) {
      const start = node.textContent.indexOf(FIND);
      const range = document.createRange();
      range.setStart(node, start);
      range.setEnd(node, start + FIND.length);
      const sel = window.getSelection();
      sel.removeAllRanges();
      sel.addRange(range);
      document.execCommand('insertText', false, REPLACE);
      return 'replaced in: ' + node.textContent.substring(0, 80);
    }
  }
  return 'not found';
}});
```

**Step 3 — Verify the change appeared** by taking a snapshot of the affected paragraph and confirming the new text is present.

**Step 4 — Trigger auto-save:**
```js
await mcp__playwright__browser_press_key({ key: 'End' });
```

**Notes:**
- This replaces only the **first** occurrence. To replace all, loop and repeat until `'not found'` is returned.
- Works on plain text, text inside `<strong>`, `<em>`, `<code>` — any visible text node.
- If the article title needs changing, use the `editorTitleParagraph` test ID: click it, then `Home` + `Shift+End` to select, then `execCommand('insertText')`.

---

## Batch Operations

To update series links across ALL articles in `medium-public-url.json`:
1. Read the JSON file to get all `editId` values
2. For each `editId`, run `update-series-links {editId}`
3. Report a summary at the end

To get draft URLs for all articles:
1. Read `medium-public-url.json`
2. For each entry, run `get-draft-url {editId}`
3. Update `medium-public-url.json` with any new `draftUrl` values
