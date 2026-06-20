# UX Review V2 — Glasstrut Frontend

> **Scope:** `frontend/` — SPA (vanilla JS + Tailwind CDN + PWA)
> **Date:** 2026-06-15
> **Reviewer:** opencode

---

## Contents

1. [Visual Design & Branding](#1-visual-design--branding)
2. [Layout & Spacing](#2-layout--spacing)
3. [Typography & Readability](#3-typography--readability)
4. [Navigation & Information Architecture](#4-navigation--information-architecture)
5. [Interaction & Feedback](#5-interaction--feedback)
6. [Mobile UX](#6-mobile-ux)
7. [Desktop UX](#7-desktop-ux)
8. [Accessibility](#8-accessibility)
9. [Performance](#9-performance)
10. [PWA & Offline](#10-pwa--offline)
11. [Edge Cases & Defensive UX](#11-edge-cases--defensive-ux)
12. [Detail Pass: Screen by Screen](#12-detail-pass-screen-by-screen)

---

## 1. Visual Design & Branding

| # | Finding | Severity | Suggestion |
|---|---------|----------|------------|
| 1.1 | **No Tailwind theme config.** Uses raw Tailwind CDN defaults — indigo-600 primary, slate neutrals, green success, amber warning. Coherent but lacks brand distinctiveness. | Low | Consider a custom `<script>` block with `tailwind.config = { theme: { extend: { colors: { brand: {...} } } } }` to lock in a named palette. |
| 1.2 | **No dark mode.** The entire UI is light-only. Users viewing at night or in dim environments get a bright white/slate experience. | Medium | Tailwind CDN supports `prefers-color-scheme: dark` detection. A dark-mode toggle or auto-switch would reduce eye strain significantly. |
| 1.3 | **No loading/skeleton states.** Every data-dependent view goes from blank/empty to populated in one jump. The chronicle feed, challenge lists, and treasury all flash empty before content arrives. | High | Add skeleton placeholder cards or at minimum a centered spinner during first load. The `Loading...` text in achievements is the only skeleton-like affordance — inconsistent. |
| 1.4 | **Confetti animation is charming but heavy.** 40 DOM elements created and destroyed on every points-earned event. On low-end phones this causes visible jank. | Low | Reduce count to 20–25 or use a canvas-based particle system. |
| 1.5 | **`alert()` dialogs for errors feel jarring.** When an activity log, challenge save, or family action fails, the app calls `alert()` — a modal dialog with no styling, no branding. This breaks immersion. | High | Replace all `alert()` calls with `showToast()` with `type: "error"` and a longer duration (or persistent until dismissed). |
| 1.6 | **Toast notifications lack swipe-to-dismiss and stack limit.** The toast container can grow unbounded if multiple toasts fire rapidly. Dismiss is only via the × button or timeout. | Medium | Cap visible toasts at 3, push older ones up/out. Add swipe-to-dismiss gesture. |

---

## 2. Layout & Spacing

| # | Finding | Severity | Suggestion |
|---|---------|----------|------------|
| 2.1 | **Content width is capped at `max-w-lg` (32rem / 512px).** On desktop this leaves large gutters on each side. The app feels like a mobile app stretched to fill a desktop browser. | Medium | On viewports >768px, consider expanding to `max-w-2xl` or adding a two-column layout (e.g. sidebar nav + content). |
| 2.2 | **Bottom navigation bar is always centered.** On wide screens, `max-w-lg` + `left-1/2 -translate-x-1/2` keeps it centered, but the active-tab indicator only communicates via color (no underline/highlight bar). | Low | Add a sliding underline indicator for the active tab to improve visual feedback. |
| 2.3 | **Profile tab is a dump of unrelated sections.** User info, achievements, family management, and logout are stacked vertically with only `<hr>` dividers. The family management section is disproportionately large relative to its usage frequency. | Medium | Consider moving family management to a sub-page or collapsible sections. Achievements could be the hero element on profile. |
| 2.4 | **Quick Log section can scroll to 50+ activities.** The "Show all" toggle reveals a `max-h-80 overflow-y-auto` list that can be very tall. On mobile this creates a long page. | Low | Consider paginating or filtering quick-log activities by challenge. |
| 2.5 | **Chronicle sentinel div placed outside the feed container.** The `#chronicle-sentinel` is appended after `#chronicle-feed` using `after()`, which works but could break if layout changes. | Low | Place sentinel inside the feed container as the last child. |

---

## 3. Typography & Readability

| # | Finding | Severity | Suggestion |
|---|---------|----------|------------|
| 3.1 | **Bottom nav labels use `text-[10px]`.** Extremely small, below Apple's 11px HIG minimum. Users with visual impairments may struggle. | Medium | Increase to `text-xs` (12px) and reduce icon sizes slightly to compensate. |
| 3.2 | **No custom font loading.** Uses system `font-sans`. Consistent and performant but lacks brand personality. | Low | If desired, load a single variable font (e.g. Inter or Outfit) via Google Fonts for a more polished feel. |
| 3.3 | **Line lengths in challenge descriptions can exceed 80 characters per line** at `max-w-lg`, harming readability on desktop. | Low | Constrain description text to `max-w-prose` or similar. |

---

## 4. Navigation & Information Architecture

| # | Finding | Severity | Suggestion |
|---|---------|----------|------------|
| 4.1 | **No back gesture or browser back-button integration.** Tab changes, modals, and challenge progress views are not reflected in `history.pushState`. Pressing the browser back button exits the app entirely. | High | Integrate `history.pushState` for tab switches, challenge detail views, and modal open/close so the back button navigates predictably. |
| 4.2 | **Challenge progress view is appended inline below the quest list.** After tapping a challenge, the user must scroll down to find the progress view, then tap "Close" to return to the list. The list itself doesn't scroll to the top. | Medium | Consider sliding the challenge detail over the list (panel slide-in) or scrolling the list to top after close. |
| 4.3 | **No persistent breadcrumb or "you are here" indicator.** In a 4-tab app this is acceptable, but with the inline challenge detail and wizard modal, users can lose context. | Low | Ensure the active tab always retains its highlight even when a modal or sub-view is open. |
| 4.4 | **Family management on the Profile tab is hard to find.** A new user's first instinct after joining a family might be to look under "Quests" or "Home". | Low | Could show a "My Family" badge or quick-link on the Home tab. |
| 4.5 | **No onboarding walkthrough.** New users see an empty state immediately after registration. The empty-state messages are helpful but a 3-step guided tour would reduce drop-off. | Medium | Consider a lightweight coachmark overlay on first login (check localStorage for a `hasSeenOnboarding` flag). |

---

## 5. Interaction & Feedback

| # | Finding | Severity | Suggestion |
|---|---------|----------|------------|
| 5.1 | **No disable/loading state on primary action buttons.** The login/register button, log-activity buttons, and QR redemption button are all single-click with no visual feedback until the network resolves. Users may tap repeatedly, causing multiple submissions. | High | Set `disabled` + change text to spinner on all async buttons immediately on click. This is already done for delete and redeem but not for login, log activity, or challenge save. |
| 5.2 | **Inline email validation is helpful but inconsistent.** Only checks for `@` and `.` — no check for domain existence or format. The green "✅ Valid Email" can appear for addresses like `a@b`. | Low | Add a more robust regex (e.g. `/^[^\s@]+@[^\s@]+\.[^\s@]+$/`). |
| 5.3 | **Form field focus indicators are inconsistent.** Some inputs use `focus:ring-2 focus:ring-indigo-300 outline-none`, others (especially dynamically created ones in activities/prizes) omit these classes. | Medium | Audit all inputs to ensure consistent focus-ring styling. |
| 5.4 | **Wizard step progression has no validation.** The user can click "Next" on step 1 without filling in the challenge title. They won't discover the error until the submit fails on step 4. | High | Validate required fields before allowing step transitions. At minimum, validate step 1 (title) before enabling "Next". |
| 5.5 | **Goal removal warning uses `confirm()` dialog.** Another native dialog that breaks the visual theme. | Medium | Replace with a styled confirmation modal similar to the delete challenge flow. |
| 5.6 | **No haptic or tactile feedback on supported devices.** Mobile users get no vibration feedback on activity log or redemptions. | Low | Call `navigator.vibrate(10)` on successful log/redeem events. |
| 5.7 | **No undo for activity log.** Once logged, the only recourse is to manually edit the entry (if it's your own and within the same session). | Medium | Consider a 3-second "undo" toast after logging (like Gmail's undo send pattern). |

---

## 6. Mobile UX

| # | Finding | Severity | Suggestion |
|---|---------|----------|------------|
| 6.1 | **Bottom nav safe-area handling is present but minimal.** Uses `.pb-safe` which only adds `padding-bottom: env(safe-area-inset-bottom)`. Home indicator areas are covered. | Low | Also apply `padding-left/right: env(safe-area-inset-left/right)` to the nav for phones with side notches. |
| 6.2 | **QR scanner works well** — `facingMode: "environment"`, `aspect-ratio: 4/3`, real-time frame decoding. The camera request flow is clean. | N/A | Praise item. |
| 6.3 | **Up Next horizontal snap-scroll is smooth on mobile.** Cards are 256px wide with `snap-start` and hidden scrollbars. | N/A | Praise item. |
| 6.4 | **Numeric input fields use `inputmode="decimal"`.** Correctly triggers numeric keypad on mobile. | N/A | Praise item. |
| 6.5 | **Keyboard avoidance for modals is not handled.** The challenge wizard modal with 6+ input fields can push content behind the virtual keyboard on mobile. The modal uses `max-h-[90vh] overflow-y-auto` which helps but doesn't reposition. | Medium | Consider listening to `window.visualViewport` resize events and scrolling the active input into view. This is a common pain point. |
| 6.6 | **Touch targets on dynamically added rows are small.** In the wizard, activity rows have `py-1.5 px-1` inputs and 10pt text — below the recommended 44pt minimum touch target. | Medium | Increase minimum touch-target size on all form controls, especially in dynamic wizard rows. |

---

## 7. Desktop UX

| # | Finding | Severity | Suggestion |
|---|---------|----------|------------|
| 7.1 | **No keyboard shortcuts.** Desktop users frequently expect shortcuts like `Escape` to close modals, `Ctrl+Enter` to submit, `/` to focus search (if any). | Low | Add global keyboard listener for `Escape` to close any open modal. |
| 7.2 | **No hover state differentiation on cards.** Challenge cards have `cursor-pointer` but no hover transform or shadow change. Desktop users get no hover feedback. | Low | Add `hover:shadow-md hover:-translate-y-0.5 transition-all` to interactive cards. |
| 7.3 | **App feels narrow on desktop.** At `max-w-lg` (512px), the content width is about ⅓ of a 1440px monitor. | Medium | See 2.1 — consider widening the breakpoint or using a responsive sidebar layout. |
| 7.4 | **The QR print feature opens a popup window.** Some browsers block this if not triggered by a user gesture. The `alert()` fallback ("Please allow popups") is acceptable but not ideal. | Low | Could also offer a "download QR as PNG" option using `<a download>`. |

---

## 8. Accessibility

| # | Finding | Severity | Suggestion |
|---|---------|----------|------------|
| 8.1 | **No ARIA live regions.** Dynamic content (toasts, chronicle feed, challenge progress) is announced to screenreaders only by chance. | High | Add `role="status" aria-live="polite"` to `#toast-container` and `role="feed"` to `#chronicle-feed`. |
| 8.2 | **No focus management.** When a modal opens, focus stays on the trigger element. When a modal closes, focus is lost to the body. Screenreader users are disoriented. | High | On modal open, move focus to the modal's first focusable element. On close, return focus to the trigger. |
| 8.3 | **No skip-to-content link.** Keyboard users must tab through the entire bottom nav and header to reach main content. | Medium | Add a visually-hidden skip link as the first focusable element. |
| 8.4 | **Color contrast on bottom nav inactive tabs.** `text-slate-400` (#94a3b8) on `bg-white` (#ffffff) fails WCAG AA for normal text (ratio ~2.7:1). | High | Darken inactive tab to `text-slate-500` (#64748b) minimum. |
| 8.5 | **Toast container uses `pointer-events-none`.** This prevents clicking the toast's dismiss button in some scenarios (the click passes through to elements behind). | Medium | Remove `pointer-events-none` from the container and ensure toasts themselves use `pointer-events-auto`. |
| 8.6 | **Templated HTML strings use `innerHTML` with interpolated data.** Though `escapeHtml()` is used consistently, any missed injection point could be an XSS vector. | Low | Consider using `textContent` and `createElement` for user-supplied data, or use a templating approach (e.g. lit-html). |
| 8.7 | **No `lang` attribute on dynamically created print document.** The print window creates a bare HTML document without `lang="en"`. | Low | Add `lang="en"` to the print template. |

---

## 9. Performance

| # | Finding | Severity | Suggestion |
|---|---------|----------|------------|
| 9.1 | **Tailwind CDN is ~3MB uncompressed.** Loaded on every page view with no cache-busting. This is the single largest performance bottleneck, especially on slow connections. | High | Consider switching to a pre-built Tailwind CSS file (only used classes) during a build step, or at minimum enable the CDN's `html` option to cache aggressively. |
| 9.2 | **`jsQR` library is loaded on every page load** even if the user never opens the scanner. Adds ~30KB gzipped. | Medium | Lazy-load `jsQR` only when the scanner is opened (dynamic `<script>` injection or `import()`). |
| 9.3 | **No image/asset optimization.** The SVG icon is small, but if raster icons are added later they should be optimized. | Low | Use `svgo` for SVG optimization. |
| 9.4 | **No request deduplication.** If `loadAllData()` is called twice rapidly (e.g. after logging + after challenge save), parallel requests for the same endpoints fire. Could cause race conditions in cachedProgressMap. | Medium | Add a simple in-flight request map to deduplicate concurrent identical GET requests. |

---

## 10. PWA & Offline

| # | Finding | Severity | Suggestion |
|---|---------|----------|------------|
| 10.1 | **Service worker caches `./` and `./index.html` as separate entries.** This could cause double caching on some servers. | Low | Use a single entry `./index.html` and serve it for both routes. |
| 10.2 | **No splash screen or startup screen.** On slow networks, the PWA shows a white screen until the SW activates and serves cached content. | Medium | Add a themed splash screen (indigo background with logo) via the manifest's `start_url` loading a minimal inline-styled HTML page. |
| 10.3 | **No manifest shortcuts.** Users on Android cannot add quick actions (e.g. "New Challenge", "Scan QR") via long-press on the app icon. | Low | Add `shortcuts` to manifest.json for common actions. |
| 10.4 | **Cache invalidation relies on version bump in `sw.js`.** When the version changes (`glasstrut-v3`), the old cache is deleted on activate. This works but requires manual versioning. | N/A | Acceptable for the project's scope. Consider adding an auto-increment build step if the project grows. |
| 10.5 | **No fallback offline page.** If the user navigates to an uncached page while offline, the SW returns a network error. | Low | Cache a minimal `offline.html` that says "You're offline" with a retry button. |
| 10.6 | **Offline queue preserves mutations but doesn't show pending state.** Queued activities are invisible to the user until they come back online and see a "Sync Complete" toast. | Medium | Show a visual indicator (e.g. "📤 2 pending") in the header or nav when offline queue is non-empty. |

---

## 11. Edge Cases & Defensive UX

| # | Finding | Severity | Suggestion |
|---|---------|----------|------------|
| 11.1 | **No rate-limit feedback on rapid activity logging.** If a user taps "Log Activity" multiple times rapidly, multiple submissions can fire (though the API may reject duplicates). | Medium | Disable the submit button immediately and re-enable only after response (see 5.1). |
| 11.2 | **`localStorage` quota exceeded is silently caught.** In private browsing (Safari) or when storage is full, the `try/catch` silently discards cache writes — user has no indication. | Low | Check `navigator.storage.estimate()` periodically and warn user if storage is nearly full. |
| 11.3 | **Session expiry (401) force-reloads the page.** The `apiFetch` function calls `window.location.reload()` on 401, which clears all in-memory state. A logged-in user mid-flow loses their work. | Medium | Instead of instant reload, show a "Session expired" toast and redirect to auth after a brief delay, or use a silent token refresh flow. |
| 11.4 | **No confirmation on logout.** Tapping the logout button immediately logs out with no "Are you sure?" prompt. | Medium | Add a styled confirmation modal: "Log out of Glasstrut? Your data is saved." |
| 11.5 | **Empty state for chronicle feed shows only after initial fetch.** If the API is slow, the chronicle area is blank (not even a loader). | Medium | Show a subtle "Loading chronicle..." placeholder during fetch. |

---

## 12. Detail Pass: Screen by Screen

### Auth Screen

| # | Finding | Severity | Suggestion |
|---|---------|----------|------------|
| A1 | Password field has no visibility toggle. Users cannot confirm what they typed. | High | Add a show/hide password button. |
| A2 | No password-strength indicator on register. | Low | Add basic strength check (length, symbol, digit). |
| A3 | No "Forgot password?" link. | High | Add "Forgot password?" link (will need a backend endpoint). |
| A4 | Toggle animation is instant — no transition between Login/Register forms. | Low | Add a subtle fade/slide transition. |

### Home Tab

| # | Finding | Severity | Suggestion |
|---|---------|----------|------------|
| H1 | Points display currency symbol defaults to 🍦. Cute but meaningless unless explained. | Low | Tooltip or small text showing that 🍦 = total across all challenge currencies. |
| H2 | "Up Next" cards don't indicate which challenge type they are visually (Personal vs Family). The badge is small text. | Low | Add a subtle background tint or icon prefix. |
| H3 | Quick Log shows raw challenge title in small text — could clash with activity names on small screens. | Low | Truncate with ellipsis more aggressively. |
| H4 | "Live" badge on Family Chronicles is always shown, even if there's no activity for hours. | Low | Could show "Updated Xm ago" instead when the last entry is stale. |

### Quests Tab

| # | Finding | Severity | Suggestion |
|---|---------|----------|------------|
| Q1 | Edit/Delete buttons appear on hover (desktop) or as always-visible ✏️/🗑️ icons (mobile). On mobile they clutter the card. | Low | Consider a "..." overflow menu or swipe-to-reveal pattern for mobile. |
| Q2 | Challenge progress panel has no loading state between challenges. Tapping a second challenge while the first is open causes a visible blank flash. | Medium | Show a spinner in the progress panel during loading. |
| Q3 | The "Close" button on challenge progress is small text. Hard to hit on mobile. | Low | Make it a proper button with generous padding. |
| Q4 | Family member tabs use the email prefix — if multiple members share the same prefix (e.g. `alice@...` and `alice2@...`), they're ambiguous. | Low | Show first name or a truncated email instead. |

### Treasury Tab

| # | Finding | Severity | Suggestion |
|---|---------|----------|------------|
| T1 | "Scan QR Code" button is large but visually flat (indigo-100 bg). Could be more prominent. | Low | Use a gradient or solid indigo-600 button for visual weight. |
| T2 | Prize list hides QR button when `hasQR` is false. But there's no "No QR available" indicator — the user sees a plain list item and doesn't know QR exists as a feature. | Low | Add a muted "No QR" badge or disable the QR button. |
| T3 | Claim history is fetched per-challenge in a loop. Could be a single endpoint. | Medium | Server-side: add `/api/claims` endpoint for all claims across challenges. |

### Profile Tab

| # | Finding | Severity | Suggestion |
|---|---------|----------|------------|
| P1 | Achievements list shows all or nothing. No filtering or sorting. | Low | Add sort by date (newest first). |
| P2 | Family management forms have no validation feedback. Creating a family with a blank name or joining with an invalid code shows an `alert()`. | High | Replace `alert()` with inline error messages near the form fields. |
| P3 | No "Leave family" for members (only shows code and members). | Medium | Add a "Leave family" button for non-owner members. |
| P4 | The avatar initial uses the username's first letter. On register, this is shown immediately. If the username is not set, it falls back to `?` — inconsistent. | Low | Always derive from email prefix as a fallback chain. |

### Challenge Modal / Wizard

| # | Finding | Severity | Suggestion |
|---|---------|----------|------------|
| W1 | Wizard step indicators are thin (h-2) colored bars. Users might not realize they're clickable indicators. Currently they're not clickable. | Low | Either make them clickable (navigate to that step) or add a step counter label like "Step 2 of 4". |
| W2 | No "Discard changes?" prompt on modal close. If a user has filled in 3 steps and accidentally closes, everything is lost. | High | Ask for confirmation when closing the modal with unsaved changes. |
| W3 | Goal advanced options (hidden, per-entry) are hidden behind a ⚙️ toggle. Many users may miss these powerful features entirely. | Low | Consider making "Hidden" a more visible checkbox, and keep "Per-entry" under advanced only. |
| W4 | Activity rows in the wizard have small touch targets (see 6.6). The remove button is especially small (`text-sm leading-none ×`). | Medium | Increase all interactive element sizes in dynamic rows. |
| W5 | The "Linked goal" dropdown on prizes lists goals by description text. If descriptions are long, the dropdown becomes unreadable. | Low | Truncate descriptions in the dropdown (CSS `text-overflow: ellipsis`). |

### QR Scanner

| # | Finding | Severity | Suggestion |
|---|---------|----------|------------|
| S1 | Scanner uses `facingMode: "environment"` but doesn't check if multiple cameras are available. Users with multiple rear cameras may get the wrong one. | Low | Use `navigator.mediaDevices.enumerateDevices()` to let users pick a camera. |
| S2 | Scanner has no manual entry fallback. If the camera doesn't work or the QR is damaged, there's no way to type in a claim code. | Medium | Add a "Enter code manually" link below the scanner view. |

---

## Severity Key

| Level | Meaning | Count |
|-------|---------|-------|
| **High** | Impacts core usability, accessibility, or data integrity. Should be addressed before next release. | 14 |
| **Medium** | Significantly improves UX but not blocking. Worth doing in the current or next cycle. | 17 |
| **Low** | Polish and refinement. Nice-to-haves. | 22 |

**Total findings: 53**

---

## Quick Wins (High Impact, Low Effort)

1. Replace `alert()` with styled toasts (5.1 regs 5.5, P2 — touches 4+ files)
2. Add button loading/disabled states to all async actions (5.1 — ~8 buttons)
3. Add focus management to modals (8.2 — ~20 lines of JS)
4. Add Escape key to close modals (7.1 — ~5 lines of JS)
5. Replace bottom nav inactive color with `text-slate-500` (8.4 — 1 CSS class change)
6. Add unsaved-changes warning on wizard close (W2 — ~10 lines of JS)
7. Add password visibility toggle (A1 — ~5 lines of HTML/JS)
8. Disable rapid resubmission of activity log (11.1 — 2 lines per button)

---

*End of review.*
