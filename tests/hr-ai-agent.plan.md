# HR AI Agent Web Application Test Plan

## Application Overview

The HR AI Agent is a Blazor Server (.NET 8 Blazor United) web application that serves as a Position Description Builder. It features local Identity authentication (email/password), a multi-session AI chat interface powered by an MCP-connected AI agent, a Quill-based rich text draft editor, and document export capabilities (Word .docx, Markdown .md, JSON .json). The app layout uses SSR for the shell (sidebar, topbar) and InteractiveServer mode for the workspace body only. The sessions sidebar uses Blazor NavigationManager for routing — sidebar onclick handlers only fire within the Blazor circuit, so session navigation in tests must use direct page.goto() calls. Key routes: / and /workspace/{sessionId:guid} (both map to DraftWorkspace.razor), /login, /register, /logout. All workspace routes require authentication and redirect to /login with a ReturnUrl parameter when unauthenticated.

## Test Scenarios

### 1. Authentication

**Seed:** `tests/seed.spec.ts`

#### 1.1. Login with valid credentials redirects to workspace

**File:** `tests/auth/login-valid.spec.ts`

**Steps:**
  1. Navigate to http://localhost:5000/login
    - expect: Page URL is http://localhost:5000/login or http://localhost:5000/login?ReturnUrl=%2F
    - expect: A 'Sign In' heading is visible
    - expect: Email input with placeholder 'you@example.com' is present
    - expect: Password input with placeholder 'Password' is present
    - expect: A 'Sign In' button is visible
    - expect: A 'Register' link pointing to /register is visible
    - expect: The sidebar shows 'No conversations yet.'
  2. Fill the email field with 'fuji.nguyen@workcontrol.com'
  3. Fill the password field with 'gasoline87'
  4. Click the 'Sign In' button
    - expect: Page redirects to http://localhost:5000/
    - expect: The user email 'fuji.nguyen@workcontrol.com' is displayed in the topbar
    - expect: A 'Sign out' link is visible in the topbar
    - expect: The 'Position Description Builder' heading is visible
    - expect: The 'Writing Assistant' section heading is visible
    - expect: The chat thread shows 'Chat responses appear here after you send a prompt.'
    - expect: The chat input textarea is visible with placeholder 'Ask for help drafting or improving your position description...'
    - expect: The Send button is visible and enabled

#### 1.2. Login with wrong password shows error message

**File:** `tests/auth/login-invalid-password.spec.ts`

**Steps:**
  1. Navigate to http://localhost:5000/login
    - expect: Sign In form is visible
  2. Fill the email field with 'fuji.nguyen@workcontrol.com'
  3. Fill the password field with 'wrongpassword'
  4. Click the 'Sign In' button
    - expect: Page URL remains at /login
    - expect: An error message 'Invalid email or password.' is displayed
    - expect: The user is NOT redirected to the workspace

#### 1.3. Login with non-existent email shows error message

**File:** `tests/auth/login-invalid-email.spec.ts`

**Steps:**
  1. Navigate to http://localhost:5000/login
    - expect: Sign In form is visible
  2. Fill the email field with 'nonexistent@example.com'
  3. Fill the password field with 'anypassword'
  4. Click the 'Sign In' button
    - expect: Page URL remains at /login
    - expect: An error message 'Invalid email or password.' is displayed

#### 1.4. Login with empty fields shows validation error

**File:** `tests/auth/login-empty-fields.spec.ts`

**Steps:**
  1. Navigate to http://localhost:5000/login
    - expect: Sign In form is visible
  2. Leave both email and password fields empty and click the 'Sign In' button
    - expect: An error message 'Please enter your email and password.' is displayed OR the browser shows HTML5 required field validation
    - expect: Page URL remains at /login

#### 1.5. Logout navigates to login page and clears session

**File:** `tests/auth/logout.spec.ts`

**Steps:**
  1. Navigate to http://localhost:5000/login and log in with email 'fuji.nguyen@workcontrol.com' and password 'gasoline87'
    - expect: User is redirected to http://localhost:5000/
    - expect: The 'Sign out' link is visible in the topbar
  2. Click the 'Sign out' link in the topbar
    - expect: Page navigates to http://localhost:5000/login
    - expect: The Sign In form is visible
    - expect: The sidebar shows 'No conversations yet.' (user session list is cleared)
    - expect: The 'Sign out' link is no longer visible
  3. Attempt to navigate directly to http://localhost:5000/
    - expect: Page redirects to http://localhost:5000/login?ReturnUrl=%2F
    - expect: The user cannot access the workspace while logged out

#### 1.6. Unauthenticated access to root redirects to login with ReturnUrl

**File:** `tests/auth/protected-route-root.spec.ts`

**Steps:**
  1. Ensure the user is logged out (navigate to http://localhost:5000/logout or start in a fresh browser context)
  2. Navigate to http://localhost:5000/
    - expect: Page redirects to http://localhost:5000/login?ReturnUrl=%2F
    - expect: The ReturnUrl query parameter is set to '/'
    - expect: The Sign In form is displayed

#### 1.7. Unauthenticated access to workspace session redirects to login with ReturnUrl

**File:** `tests/auth/protected-route-workspace.spec.ts`

**Steps:**
  1. Ensure the user is logged out
  2. Navigate directly to http://localhost:5000/workspace/7e86bffc-d5c5-4dbc-bcf4-978b49f4c179 (or any valid GUID workspace URL)
    - expect: Page redirects to http://localhost:5000/login?ReturnUrl=%2Fworkspace%2F7e86bffc-d5c5-4dbc-bcf4-978b49f4c179
    - expect: The Sign In form is displayed
    - expect: The ReturnUrl query parameter contains the original workspace path

#### 1.8. After login with ReturnUrl redirects back to the original page

**File:** `tests/auth/login-return-url.spec.ts`

**Steps:**
  1. Ensure the user is logged out
  2. Navigate to http://localhost:5000/workspace/7e86bffc-d5c5-4dbc-bcf4-978b49f4c179
    - expect: Page redirects to http://localhost:5000/login?ReturnUrl=%2Fworkspace%2F7e86bffc-d5c5-4dbc-bcf4-978b49f4c179
  3. Enter email 'fuji.nguyen@workcontrol.com' and password 'gasoline87', then click 'Sign In'
    - expect: Page redirects to http://localhost:5000/workspace/7e86bffc-d5c5-4dbc-bcf4-978b49f4c179 (the original workspace URL)
    - expect: The session conversation history is loaded in the chat thread

#### 1.9. Registration with a new unique email creates account and logs in

**File:** `tests/auth/register-new-user.spec.ts`

**Steps:**
  1. Ensure the user is logged out and navigate to http://localhost:5000/register
    - expect: A 'Create Account' heading is visible
    - expect: An email input with placeholder 'you@example.com' is present
    - expect: A password input with placeholder 'At least 6 characters' is present
    - expect: A 'Create Account' button is visible
    - expect: An 'Already have an account? Sign in' link pointing to /login is visible
  2. Fill the email field with a unique test email such as 'testuser-{timestamp}@example.com'
  3. Fill the password field with a valid password of at least 6 characters, e.g. 'Password123!'
  4. Click the 'Create Account' button
    - expect: Page redirects to http://localhost:5000/
    - expect: The user is now logged in
    - expect: The user's email is displayed in the topbar
    - expect: The 'Sign out' link is visible
    - expect: The sidebar shows 'No conversations yet.' for the new account

#### 1.10. Registration with an already existing email shows error

**File:** `tests/auth/register-duplicate-email.spec.ts`

**Steps:**
  1. Ensure the user is logged out and navigate to http://localhost:5000/register
    - expect: Create Account form is visible
  2. Fill the email field with an already registered email 'fuji.nguyen@workcontrol.com'
  3. Fill the password field with 'gasoline87'
  4. Click the 'Create Account' button
    - expect: Page URL remains at /register
    - expect: An error message is displayed (e.g. 'Username fuji.nguyen@workcontrol.com is already taken.')
    - expect: The user is NOT logged in or redirected

#### 1.11. Registration with a short password shows validation error

**File:** `tests/auth/register-short-password.spec.ts`

**Steps:**
  1. Navigate to http://localhost:5000/register
    - expect: Create Account form is visible
  2. Fill the email field with a unique email address
  3. Fill the password field with a password of fewer than 6 characters, e.g. 'abc'
  4. Click the 'Create Account' button
    - expect: Page URL remains at /register
    - expect: An error message is displayed describing the password requirements
    - expect: Account is NOT created

#### 1.12. Registration with empty fields shows validation error

**File:** `tests/auth/register-empty-fields.spec.ts`

**Steps:**
  1. Navigate to http://localhost:5000/register
    - expect: Create Account form is visible
  2. Leave both email and password fields empty and click 'Create Account'
    - expect: An error message 'Please enter your email and password.' is displayed OR the browser shows HTML5 required field validation
    - expect: Page URL remains at /register

### 2. Session Management

**Seed:** `tests/seed.spec.ts`

#### 2.1. New workspace page shows empty chat with placeholder text

**File:** `tests/sessions/new-workspace-empty-state.spec.ts`

**Steps:**
  1. Log in as 'fuji.nguyen@workcontrol.com' and navigate to http://localhost:5000/
    - expect: Page loads at http://localhost:5000/
    - expect: The 'Position Description Builder' heading is visible
    - expect: The 'Writing Assistant' section is visible
    - expect: The chat thread shows the text 'Chat responses appear here after you send a prompt.'
    - expect: The chat input textarea has the placeholder 'Ask for help drafting or improving your position description...'
    - expect: The Send button is visible and enabled
    - expect: The right-editor draft panel is NOT visible (single-column layout)
    - expect: The 'Export' button is NOT visible
    - expect: The 'Hide Chat' button is NOT visible in the topbar

#### 2.2. Sending a prompt on new workspace creates a new session and navigates to workspace URL

**File:** `tests/sessions/new-session-creation.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/ to open a fresh workspace
    - expect: Page is at http://localhost:5000/
    - expect: Chat thread is empty with placeholder text
  2. Click on the chat input textarea (selector: textarea.chat-input)
    - expect: Textarea is focused
  3. Type a non-draft prompt such as 'list open positions'
  4. Click the 'Send' button (selector: button.primary-btn)
    - expect: The URL changes to http://localhost:5000/workspace/{new-guid} where {new-guid} is a new session GUID
    - expect: The user message 'list open positions' appears in the chat thread with a 'You:' label
    - expect: The new session appears at the top of the sidebar sessions list with the prompt text as its name

#### 2.3. Clicking + New button navigates to the root new workspace

**File:** `tests/sessions/new-chat-button.spec.ts`

**Steps:**
  1. Log in and navigate to an existing session URL such as http://localhost:5000/workspace/7e86bffc-d5c5-4dbc-bcf4-978b49f4c179
    - expect: The session conversation history is displayed in the chat thread
  2. Click the '+ New' button in the sidebar header
    - expect: Page navigates to http://localhost:5000/
    - expect: The chat thread shows 'Chat responses appear here after you send a prompt.' (empty state)
    - expect: No session is marked as active in the sidebar
    - expect: The URL is http://localhost:5000/

#### 2.4. Navigating directly to an existing session URL loads conversation history

**File:** `tests/sessions/load-existing-session.spec.ts`

**Steps:**
  1. Log in as 'fuji.nguyen@workcontrol.com'
  2. Navigate directly to http://localhost:5000/workspace/7e86bffc-d5c5-4dbc-bcf4-978b49f4c179 (a session that has at least one prompt/response pair)
    - expect: Page loads at the session URL
    - expect: The conversation history is displayed in the chat thread
    - expect: The user message 'test navigation - what is a job description?' is visible with a 'You:' label
    - expect: The assistant response about job descriptions is visible with an 'Assistant:' label
    - expect: The correct session is highlighted with 'session-item--active' CSS class in the sidebar

#### 2.5. Navigating to a non-existent session GUID redirects to root

**File:** `tests/sessions/invalid-session-redirect.spec.ts`

**Steps:**
  1. Log in as 'fuji.nguyen@workcontrol.com'
  2. Navigate to http://localhost:5000/workspace/00000000-0000-0000-0000-000000000000
    - expect: Page redirects to http://localhost:5000/
    - expect: The chat thread shows the empty state placeholder text
    - expect: No session is highlighted in the sidebar

#### 2.6. Active session is highlighted in the sidebar

**File:** `tests/sessions/active-session-highlight.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/workspace/7e86bffc-d5c5-4dbc-bcf4-978b49f4c179
  2. Inspect the sidebar sessions list
    - expect: The session item for the current session has the CSS class 'session-item--active'
    - expect: Other session items do NOT have the 'session-item--active' class
    - expect: The active session name matches the current session's name

#### 2.7. Session can be renamed by double-clicking the session name

**File:** `tests/sessions/rename-session.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/workspace/7e86bffc-d5c5-4dbc-bcf4-978b49f4c179 or the root to see the sessions list
    - expect: At least one session is listed in the sidebar
  2. Double-click the session name span (selector: .session-name) on any session in the sidebar
    - expect: The session name span is replaced by a text input field (selector: .session-rename-input)
    - expect: The input is pre-filled with the current session name
  3. Clear the input and type a new name such as 'Renamed Session'
    - expect: The input updates to show 'Renamed Session'
  4. Press Enter to commit the rename
    - expect: The input is replaced by the session name span
    - expect: The session name in the sidebar now shows 'Renamed Session'

#### 2.8. Session rename can be cancelled with Escape key

**File:** `tests/sessions/rename-session-escape.spec.ts`

**Steps:**
  1. Log in and double-click a session name in the sidebar to enter rename mode
    - expect: The rename input is visible and focused
  2. Type a new name but then press Escape
    - expect: The rename input is dismissed
    - expect: The session name reverts to its original value
    - expect: No rename is persisted

#### 2.9. Session can be deleted with the delete (x) button

**File:** `tests/sessions/delete-session.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/ so at least one session is visible in the sidebar
    - expect: At least one session appears in the sidebar list
  2. Note the session name of a session you will delete. Do NOT delete the session at http://localhost:5000/workspace/7e86bffc-d5c5-4dbc-bcf4-978b49f4c179 to preserve test data. Instead delete a disposable session.
  3. Click the '✕' delete button (selector: button.ghost-btn.ghost-btn--icon with title='Delete') next to a session that is NOT the currently active session
    - expect: The session is removed from the sidebar list
    - expect: The current workspace URL and chat content are unchanged

#### 2.10. Deleting the currently active session redirects to root

**File:** `tests/sessions/delete-active-session.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/ then type a short prompt such as 'show tools' and click Send to create a disposable new session
    - expect: A new session is created and URL changes to /workspace/{new-guid}
  2. Wait for the URL to change to the new session URL, then click the '✕' delete button next to the currently active session (the one with 'session-item--active' class)
    - expect: Page navigates to http://localhost:5000/
    - expect: The deleted session is no longer listed in the sidebar
    - expect: The chat thread shows the empty state placeholder text

### 3. Chat and Prompt Sending

**Seed:** `tests/seed.spec.ts`

#### 3.1. Send button is enabled with non-empty text in textarea

**File:** `tests/chat/send-button-enabled.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/
    - expect: The Send button is visible
  2. Click the chat input textarea and type 'Hello'
    - expect: The Send button (selector: button.primary-btn) is not disabled

#### 3.2. Sending an empty prompt does not create a session

**File:** `tests/chat/send-empty-prompt.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/
    - expect: Chat thread shows empty state placeholder
    - expect: URL is http://localhost:5000/
  2. Click the Send button without typing anything in the textarea
    - expect: No new session is created
    - expect: URL remains at http://localhost:5000/
    - expect: Chat thread still shows empty state placeholder
    - expect: No user message bubble appears in the chat thread

#### 3.3. Pressing Enter in chat textarea sends the prompt

**File:** `tests/chat/send-with-enter-key.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/
    - expect: Chat thread is empty
  2. Click the chat textarea and type a prompt such as 'list open positions'
  3. Press the Enter key (without Shift)
    - expect: The prompt is submitted
    - expect: URL changes to /workspace/{new-guid}
    - expect: The user message appears in the chat thread with 'You:' label
    - expect: The textarea is cleared

#### 3.4. Pressing Shift+Enter in chat textarea adds a newline instead of sending

**File:** `tests/chat/shift-enter-newline.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/
    - expect: Chat textarea is empty
  2. Click the chat textarea and type 'First line'
  3. Press Shift+Enter
    - expect: A newline is inserted into the textarea
    - expect: The prompt is NOT submitted
    - expect: URL remains at http://localhost:5000/
  4. Type 'Second line'
    - expect: The textarea now contains 'First line\nSecond line' (two lines of text)

#### 3.5. User message appears immediately after sending (before AI responds)

**File:** `tests/chat/user-message-immediate.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/
  2. Type 'list open positions' in the chat input and click Send
    - expect: Immediately after clicking Send, the user message 'list open positions' appears in the chat thread under a 'You:' label styled with the CSS class 'chat-bubble-row--user'
    - expect: The textarea is cleared
    - expect: URL changes to /workspace/{new-guid}

#### 3.6. Loading indicator appears while AI is generating a response

**File:** `tests/chat/loading-indicator.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/
  2. Type 'list open positions' in the chat input and click Send
    - expect: Within a short time after clicking Send, a loading indicator (selector: .chat-bubble--loading) appears in the chat thread
    - expect: The loading bubble contains a spinner element (selector: .chat-spinner) and the text 'Assistant is drafting your response...'
    - expect: The loading bubble has the role='status' and aria-live='polite' ARIA attributes

#### 3.7. Send button is disabled while AI is generating a response

**File:** `tests/chat/send-button-disabled-during-response.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/
  2. Type 'list open positions' in the chat input and click Send
    - expect: While the loading indicator is visible (AI is busy), the Send button (selector: button.primary-btn) has the disabled attribute set
    - expect: The button cannot be clicked again to send another prompt

#### 3.8. Help icon shows tooltip with keyboard shortcut information

**File:** `tests/chat/help-icon-tooltip.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/
  2. Locate the '?' help icon (selector: span.help-icon) in the chat composer area
    - expect: The '?' element is visible
  3. Inspect the title attribute of the '?' element
    - expect: The title attribute contains the text 'Enter to send, Shift+Enter for a new line. Draft updates automatically for draft/refine prompts.'
    - expect: The aria-label attribute contains 'Help: Enter to send, Shift+Enter for a new line. Draft updates automatically for draft/refine prompts.'

#### 3.9. AI response appears in chat after receiving response

**File:** `tests/chat/ai-response-appears.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/workspace/7e86bffc-d5c5-4dbc-bcf4-978b49f4c179 to load an existing session with a completed AI response
    - expect: The session conversation history is visible
    - expect: An assistant response bubble (selector: .chat-bubble-row--assistant .chat-bubble:not(.chat-bubble--loading)) is present
    - expect: The assistant bubble contains rendered markdown content (e.g. paragraphs, bold text, lists)
    - expect: The response starts with 'Assistant:' label
    - expect: The loading indicator is NOT visible

#### 3.10. Sending a prompt from an existing session adds to the conversation

**File:** `tests/chat/continue-existing-session.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/workspace/7e86bffc-d5c5-4dbc-bcf4-978b49f4c179
    - expect: Existing conversation is loaded
  2. Type 'what are the key qualifications for this role?' in the chat input
  3. Click Send
    - expect: The new user message appears in the chat thread below the existing messages
    - expect: The URL remains at the same /workspace/{guid} URL (no new session is created)
    - expect: A loading indicator appears while the AI responds

### 4. Draft Editor and Document Workspace

**Seed:** `tests/seed.spec.ts`

#### 4.1. Draft editor panel is not visible in a new workspace before any draft prompt

**File:** `tests/editor/draft-panel-hidden-initially.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/
    - expect: The workspace layout has a single column (only the chat section, selector: section.left-chat)
    - expect: The right editor section (selector: section.right-editor) is NOT present in the DOM
    - expect: The 'Export' button is NOT visible
    - expect: The 'Hide Chat' / 'Show Chat' button is NOT visible in the topbar
    - expect: The workspace CSS grid style is 'grid-template-columns: minmax(0, 1fr);'

#### 4.2. Draft editor becomes visible after a draft-intent AI prompt and response

**File:** `tests/editor/draft-panel-appears-after-draft-prompt.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/
    - expect: Empty new workspace is shown
  2. Type a draft-intent prompt such as 'draft a job description for a Software Engineer' in the chat input and click Send. Note: this test requires a live AI response and may take 30-120 seconds. Use test.setTimeout(150000).
  3. Wait for the AI response to complete (the loading indicator disappears and an assistant message appears)
    - expect: The right editor panel (selector: section.right-editor) becomes visible in the DOM
    - expect: The workspace layout changes to a two-column grid (chat | splitter | editor)
    - expect: The Quill rich text editor (selector: #quill-editor-wrapper) is visible and contains the drafted content
    - expect: The 'Your Position Description Draft' heading is visible in the right panel
    - expect: The 'Export' button becomes visible in the right panel header
    - expect: The 'Hide Chat' button becomes visible in the topbar

#### 4.3. Panel splitter is visible and resizable when both panels are open

**File:** `tests/editor/panel-splitter.spec.ts`

**Steps:**
  1. Navigate to a workspace session that already has a draft visible (a session where a draft prompt was completed and the right-editor panel is shown)
    - expect: Both the chat panel (section.left-chat) and the editor panel (section.right-editor) are visible
    - expect: A splitter element (selector: div.splitter with role='separator') is visible between the two panels
  2. Drag the splitter element horizontally to the right by about 50 pixels
    - expect: The left chat panel width increases accordingly
    - expect: The right editor panel width decreases
    - expect: Both panels remain visible after resizing
  3. Drag the splitter to a position that would make the left panel narrower than 320px
    - expect: The left panel width is clamped to a minimum of 320px (it does not go below the minimum)
  4. Drag the splitter to a position that would make the left panel wider than 760px
    - expect: The left panel width is clamped to a maximum of 760px

#### 4.4. Hide Chat button toggles chat panel visibility

**File:** `tests/editor/hide-chat-toggle.spec.ts`

**Steps:**
  1. Navigate to a workspace session where the draft panel is visible (both chat and editor panels shown)
    - expect: A 'Hide Chat' button is visible in the topbar (selector: button.ghost-btn in .workspace-topbar)
    - expect: The chat section (section.left-chat) is visible
  2. Click the 'Hide Chat' button
    - expect: The chat section is hidden (not present in DOM or display:none)
    - expect: The editor section expands to full width
    - expect: The button label changes to 'Show Chat'
    - expect: The splitter is no longer visible
  3. Click the 'Show Chat' button
    - expect: The chat section becomes visible again
    - expect: The workspace reverts to two-column layout
    - expect: The button label changes back to 'Hide Chat'

#### 4.5. Quill editor allows direct text editing of the draft

**File:** `tests/editor/quill-editor-direct-edit.spec.ts`

**Steps:**
  1. Navigate to a workspace session where the draft editor panel is visible and the Quill editor contains content
    - expect: The Quill editor is visible and contains text content
  2. Click inside the Quill editor content area (selector: #quill-editor-wrapper .ql-editor)
    - expect: The editor is focused and a cursor appears
  3. Type additional text, such as ' - edited'
    - expect: The typed text appears in the editor at the cursor position
    - expect: The content in the Quill editor is updated

#### 4.6. Quill editor toolbar is visible and contains formatting buttons

**File:** `tests/editor/quill-toolbar.spec.ts`

**Steps:**
  1. Navigate to a workspace session where the draft editor panel is visible
    - expect: The Quill editor toolbar is visible (selector: .ql-toolbar)
  2. Inspect the toolbar elements
    - expect: A heading format selector (select.ql-header) is present with options for H1, H2, H3
    - expect: A Bold button (button.ql-bold) is present
    - expect: An Italic button (button.ql-italic) is present
    - expect: An Underline button (button.ql-underline) is present
    - expect: A Strikethrough button (button.ql-strike) is present
    - expect: Ordered list button (button.ql-list[value='ordered']) is present
    - expect: Bullet list button (button.ql-list[value='bullet']) is present
    - expect: Indent buttons (ql-indent) are present
    - expect: A Background color button (button.ql-background) is present
    - expect: A Clean formatting button (button.ql-clean) is present

#### 4.7. Non-draft intent prompts do not trigger the draft panel to appear

**File:** `tests/editor/non-draft-prompt-no-editor.spec.ts`

**Steps:**
  1. Log in, navigate to http://localhost:5000/, type 'list open positions' and click Send. Wait for AI response. Use test.setTimeout(150000).
    - expect: The user message appears in chat
    - expect: AI response appears in chat after loading indicator disappears
  2. After the AI response is received, inspect the workspace
    - expect: The right editor panel (section.right-editor) is NOT visible
    - expect: The workspace remains in single-column layout
    - expect: No 'Export' button is visible
    - expect: No 'Hide Chat' button appears in the topbar

### 5. Export Functionality

**Seed:** `tests/seed.spec.ts`

#### 5.1. Export menu opens when clicking Export button in draft panel

**File:** `tests/export/export-menu-opens.spec.ts`

**Steps:**
  1. Navigate to a workspace session where the draft editor panel is visible and contains content (a session where a draft-intent prompt was completed)
    - expect: The 'Export' button (selector: button.ghost-btn with text 'Export ▾') is visible in the right panel header
  2. Click the 'Export ▾' button
    - expect: An export dropdown menu (selector: div.export-menu) appears
    - expect: The menu contains three items: 'Word (.docx)', 'Markdown (.md)', and 'JSON (.json)'

#### 5.2. Export menu closes when clicking Export button again

**File:** `tests/export/export-menu-toggle.spec.ts`

**Steps:**
  1. Navigate to a workspace session with a visible draft panel and click the 'Export ▾' button
    - expect: The export menu is visible
  2. Click the 'Export ▾' button again
    - expect: The export dropdown menu is hidden / removed from DOM

#### 5.3. Export to Word downloads a .docx file when draft has content

**File:** `tests/export/export-word.spec.ts`

**Steps:**
  1. Navigate to a workspace session where the draft panel contains content. If no such session exists, first send a draft-intent prompt and wait for the AI response (use test.setTimeout(150000)).
    - expect: The Export button is visible and the editor contains text
  2. Click the 'Export ▾' button to open the export menu
    - expect: Export menu opens with Word, Markdown, and JSON options
  3. Set up a download listener in the test (page.waitForEvent('download')), then click 'Word (.docx)'
    - expect: A file download is triggered
    - expect: The downloaded file has a .docx extension
    - expect: A success status message (containing a checkmark '✅' and the filename) appears in the export status area below the export button
    - expect: The export menu closes

#### 5.4. Export to Markdown downloads a .md file when draft has content

**File:** `tests/export/export-markdown.spec.ts`

**Steps:**
  1. Navigate to a workspace session where a draft-intent prompt has been completed (draft panel is visible with content)
    - expect: The Export button is visible
  2. Set up a download listener, open the export menu, and click 'Markdown (.md)'
    - expect: A file download is triggered
    - expect: The downloaded file is named 'position-description.md'
    - expect: A success status message '✅ Markdown file ready.' appears in the export status area
    - expect: The export menu closes

#### 5.5. Export to JSON downloads a .json file when conversation has turns

**File:** `tests/export/export-json.spec.ts`

**Steps:**
  1. Navigate to a workspace session that has at least one conversation turn (e.g., http://localhost:5000/workspace/7e86bffc-d5c5-4dbc-bcf4-978b49f4c179). The draft panel does NOT need to be visible for JSON export.
    - expect: The session loads with conversation history. Note: JSON export is only available if the draft panel and Export button are visible, which requires a draft-intent prompt to have been completed.
  2. If the Export button is visible, set up a download listener, open the export menu, and click 'JSON (.json)'
    - expect: A file download is triggered
    - expect: The downloaded file is named 'position-description.json'
    - expect: The file contains a JSON object with 'exportedAt', 'draft', and 'turns' fields
    - expect: The 'turns' array contains at least one entry with 'Role', 'Text', and 'Timestamp'
    - expect: A success status '✅ JSON file ready.' appears
    - expect: The export menu closes

#### 5.6. Export to Word with empty draft shows error status

**File:** `tests/export/export-word-empty-draft.spec.ts`

**Steps:**
  1. Navigate to a workspace session where the draft panel is visible but the Quill editor content is empty or contains only blank HTML (e.g., '<p><br></p>'). This may require manually clearing the editor after the panel becomes visible.
    - expect: The Export button is visible
  2. Click 'Export ▾' then click 'Word (.docx)'
    - expect: No file download occurs
    - expect: A status message 'Draft is empty. Add content before exporting.' appears in the export status area

#### 5.7. Export to Markdown with empty draft shows error status

**File:** `tests/export/export-markdown-empty.spec.ts`

**Steps:**
  1. Navigate to a workspace session where the draft panel is visible, but no draft markdown has been generated (the _currentDraftMarkdown internal state is empty). This can occur if a non-draft-intent response was the last message.
    - expect: The Export button is visible
  2. Click 'Export ▾' then click 'Markdown (.md)'
    - expect: No file download occurs
    - expect: A status message 'Draft is empty. Add content before exporting.' appears

#### 5.8. Export button is disabled while AI is busy generating a response

**File:** `tests/export/export-button-disabled-during-busy.spec.ts`

**Steps:**
  1. Navigate to a workspace session where the draft panel is visible
    - expect: The Export button is visible and enabled
  2. Send another prompt from the chat input and observe the UI while the AI is loading
    - expect: While the loading indicator is visible (AI is busy), the 'Export ▾' button has the 'disabled' attribute set and cannot be clicked

### 6. UI State Management

**Seed:** `tests/seed.spec.ts`

#### 6.1. Sidebar sessions list shows all user sessions sorted by recency

**File:** `tests/ui-state/sidebar-sessions-list.spec.ts`

**Steps:**
  1. Log in as 'fuji.nguyen@workcontrol.com' and navigate to http://localhost:5000/
    - expect: The sessions sidebar is visible on the left side of the page
    - expect: The 'Conversations' heading is shown at the top of the sidebar
    - expect: All existing sessions for the logged-in user are listed with their session names
    - expect: Sessions appear as list items with CSS class 'session-item'
    - expect: Each session item contains a session name span (selector: .session-name) and a delete button (selector: button.ghost-btn--icon with title='Delete')

#### 6.2. Sidebar shows 'No conversations yet.' when no sessions exist

**File:** `tests/ui-state/sidebar-empty-state.spec.ts`

**Steps:**
  1. Log in with a brand new account that has no sessions created (register a new account first if needed)
    - expect: The sessions sidebar shows the text 'No conversations yet.' (selector: .sessions-empty)
    - expect: No session items are present in the sidebar

#### 6.3. Sidebar and topbar are visible as part of the SSR layout on all pages

**File:** `tests/ui-state/layout-persistence.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/
    - expect: The sessions sidebar (selector: .sessions-sidebar) is visible
  2. Navigate to http://localhost:5000/register
    - expect: The sessions sidebar is still visible on the register page
    - expect: The topbar with user email and Sign out link is visible on the register page
  3. Navigate to http://localhost:5000/workspace/7e86bffc-d5c5-4dbc-bcf4-978b49f4c179
    - expect: The sessions sidebar is visible on the workspace session page
    - expect: The topbar with the logged-in user email is visible

#### 6.4. Chat textarea is cleared after sending a prompt

**File:** `tests/ui-state/textarea-cleared-after-send.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/
    - expect: Chat textarea is empty
  2. Type 'list open positions' in the chat textarea and click Send
    - expect: After clicking Send, the textarea is immediately cleared to empty
    - expect: The prompt text is no longer in the textarea

#### 6.5. Export status message appears after successful or failed export

**File:** `tests/ui-state/export-status-message.spec.ts`

**Steps:**
  1. Navigate to a session with a visible draft panel and trigger a JSON export (open Export menu, click 'JSON (.json)')
    - expect: A status message area (selector: .panel-status or .export-status) becomes visible
    - expect: The message contains '✅ JSON file ready.'
  2. Trigger another export action to verify the status updates
    - expect: The status message updates to reflect the new export action result

#### 6.6. Error message appears in status area on agent error

**File:** `tests/ui-state/error-status-message.spec.ts`

**Steps:**
  1. Navigate to a workspace and observe the status area (selector: .chat-item.panel-status in the right editor panel)
    - expect: Under normal conditions, the status area is empty or not visible
  2. Simulate an error condition by disconnecting from the network or observing a timeout scenario (this is an exploratory test — verify the error state displays a user-facing message beginning with 'Error:' in the status area if the AI fails)
    - expect: If an error occurs, a message starting with 'Error:' or 'Export failed:' appears in the status area
    - expect: The UI does not crash or show a blank page
    - expect: The Send button returns to an enabled state after the error

#### 6.7. Page title is 'Position Description Builder' in the workspace heading

**File:** `tests/ui-state/page-heading.spec.ts`

**Steps:**
  1. Log in and navigate to http://localhost:5000/
    - expect: The main h1 heading (selector: h1) reads 'Position Description Builder'
  2. Navigate to an existing session such as http://localhost:5000/workspace/7e86bffc-d5c5-4dbc-bcf4-978b49f4c179
    - expect: The main h1 heading still reads 'Position Description Builder'
