# Roadmap

## Phase 1 — Foundation
- [x] Initialize frontend (Vanilla JS + Tailwind static site skeleton)
- [x] Initialize .NET 10 Web API backend
- [x] Set up GitHub Pages deployment for frontend
- [x] Set up PWA manifest + service worker
- [x] Set up database schema (SQLite)

## Phase 2 — Authentication
- [x] User registration (email + password)
- [x] Login / logout
- [x] Session management / token handling (JWT)

## Phase 3 — Families
- [x] Create family
- [x] Join family (invite by code / link)
- [x] Family member management

## Phase 4 — Challenges
- [x] Create challenge (family-wide, self-only, targeted)
- [x] Optional time constraints
- [x] Multiple goals per challenge
- [x] Multiple prizes per challenge
- [x] Activities as modes of achieving goals (running, mowing, etc.)
- [x] Goal types: Achievement (target-based) or Currency (point accumulation)
- [x] Activity-based progress logging (log what you did, system calculates progress)

## Phase 5 — Goals & Achievements
- [x] Goal completion tracking
- [x] Achievement system
- [x] Achievement display

## Phase 6 — Progress & Dashboard
- [x] Challenge progress views (member tabs for family challenges, activity history log)
- [x] User dashboard (challenge summaries, achievement counts)
- [x] Activity log endpoint + frontend display
- [x] Multi-user progress endpoint (`GET /api/challenges/{id}/progress/members`)
- [ ] Notifications (optional)

## Phase 7 — QR Reward Redemption
- [x] QR code generation for prizes (server-side with QRCoder)
- [x] Static QR encodes challenge+prize IDs (`{baseUrl}/?claim=challengeId:prizeId`)
- [x] Point deduction + prize fulfillment
- [x] Printable reward coupon view
- [x] Redemption history per challenge

## Phase 8 — UI Redesign & Challenge Enhancements
- [x] Tailwind CSS via CDN replaces custom CSS
- [x] 4-tab layout with bottom navigation
- [x] Auth view redesign with login/register toggle
- [x] Home tab with greeting, points, Up Next cards, activity feed
- [x] Quests tab with challenge list + detail + activity logging
- [x] Treasury tab with prize list, claims, QR scanner
- [x] Profile tab with achievements, family management, logout
- [x] In-app QR scanner (`getUserMedia` + `jsQR`)
- [x] Hidden goals (`ChallengeGoal.IsHidden`)
- [x] Activity types (Distance/Time/DistanceAndTime/Occurrence)
- [x] DistanceAndTime dual-input progress logging
- [x] QR toggle per prize (`ChallengePrize.HasQR`)
- [x] Goal-linked prizes (`ChallengePrize.ChallengeGoalId`)
- [x] Toast notifications for hidden goal discoveries + redemptions
- [x] Challenge edit via `PUT /api/challenges/{id}` with smart-merge
- [x] Edit button visible to challenge creator
- [x] Create/edit modal with dynamic goal/prize/activity rows

## Phase 9 — Username Auth
- [x] `IdentityUser.UserName` now used as actual username (previously set to email)
- [x] Login accepts username or email
- [x] Registration accepts optional username (falls back to email prefix)
- [x] Username displayed in frontend dashboard, avatar initial uses username
- [x] JWT includes `Name` claim with username
- [x] Frontend registration form shows username input; login form hides it
- [x] Duplicate username check on registration

## Phase 10 — Currency Overhaul & Streak Counter
- [x] `ChallengeCurrencyBalance` table (per-user, per-challenge balance)
- [x] `ProgressEntry.CurrencyEarned` stores per-activity points earned
- [x] Currency earned is `amount × activity.PointValue` when challenge has `CurrencyName`
- [x] Balance upserted and deducted on activity log / prize redemption
- [x] Remove Currency goal type from create/edit modal (replaced by per-challenge currency)
- [x] RedeemService now uses `ChallengeCurrencyBalance` instead of `GoalProgress` sum
- [x] Streak counter tracks consecutive days of activity per challenge
- [x] Streak logic: same day → unchanged, yesterday → increment, earlier → reset to 1
- [x] Balance + streak displayed in self and family progress views
- [x] `currencyEarned` shown in activity log entries + toast on log
- [x] `refreshPoints()` sums `currencyBalance` across all challenges with matching currency

## Phase 11 — TimeUnit & Test Fixes
- [x] Re-added `ChallengeActivity.TimeUnit` column after it was mistakenly removed
- [x] Added `ALTER TABLE` startup SQL in Program.cs for existing databases
- [x] Added `PendingModelChangesWarning` suppression in test factory

## Phase 12 — Chronicle Redemptions, Challenge Deletion & Log Editing
- [x] Unified chronicle feed endpoint (`GET /api/chronicle`) combining activity logs and prize redemptions
- [x] Paginated chronicle feed with offset/limit + infinite scroll on frontend
- [x] Prize redemption entries appear in Family Chronicles on Home tab
- [x] Delete challenge endpoint (`DELETE /api/challenges/{id}`) with cascade delete of all related data
- [x] Delete challenge button visible to creator with styled confirmation modal
- [x] Edit progress entry endpoint (`PUT /api/challenges/{id}/activities/{aid}/log/{eid}`) with recalculate logic
- [x] Inline edit form on activity log entries (amount, time, notes)
- [x] Recalculates currency balance and goal progress on edit; handles completion/un-completion
- [ ] Notifications (optional)

## Phase 12b — Multi-Goal Activities
- [x] Many-to-many join table `ChallengeActivityGoal` linking activities to multiple goals
- [x] EF migration with data migration from old `ChallengeActivity.ChallengeGoalId`
- [x] `GoalLinks` navigation on `ChallengeActivity`; `ChallengeGoalId` removed
- [x] DTOs: `CreateActivityDto.GoalIndices`, `UpdateActivityDto.GoalIds`, `ChallengeActivityDto.GoalIds`
- [x] Activity logging loop over all linked goals (backend recalculates each)
- [x] Edit activity entry updates all linked goals
- [x] Frontend: goal checkboxes in challenge-level activity forms
- [x] Frontend: goal badges on activity log forms in progress views
- [x] Frontend: removed goal-level activities (all activities now challenge-level)
- [x] All 73 existing tests pass

## Phase 12c — Streak Goals, Per-Entry Goals & Hidden Prize Filtering
- [x] `ChallengeGoal.IsPerEntry` field — per-entry (max-based) vs accumulation (sum-based) progress
- [x] `ChallengeGoal.Type` supports "Streak" — tracks current streak as progress value
- [x] Streak goals: progress auto-syncs with `ChallengeCurrencyBalance.CurrentStreak` in progress views
- [x] Per-entry: `CurrentValue = MAX(amount × pointValue)` across all entries; edit recomputes max
- [x] Prizes linked to hidden goals are filtered from non-completing viewers in challenge list/detail
- [x] GoalService: both `LogActivityAsync` and `UpdateActivityEntryAsync` handle IsPerEntry and Streak
- [x] Frontend: Streak type in goal dropdown, IsPerEntry checkbox in advanced options, badges in progress cards
- [x] All 73 existing tests pass

## Phase 13 — UX Polish & Offline-First Client

**Status:** In progress — 47 of 64 items checked, 1 optional skipped, 16 remaining.

### 13a — High-Severity UX Fixes (UX-REVIEW-V2)

**Visual & Interaction (High):**
- [x] Replace all `alert()` calls with styled `showToast()` error toasts (findings 1.5, 5.5, P2)
- [x] Add button loading/disabled states to all async actions (login, log activity, challenge save, QR redeem — finding 5.1)
- [x] Disable rapid resubmission on activity log buttons (finding 11.1 — via `setLoading()` disabled state)
- [x] Add loading/skeleton states for data-dependent views (chronicle feed — finding 1.3)
- [x] Add password visibility toggle on auth form (finding A1)
- [x] Add "Forgot password?" link on login form (finding A3 — shows info toast)

**Wizard & Navigation (High):**
- [x] Validate required fields (title) before allowing wizard step transitions (finding 5.4)
- [x] Ask for confirmation when closing wizard with unsaved changes (finding W2)
- [ ] Integrate `history.pushState` for tab switches, challenge detail views, and modal open/close so browser back button navigates predictably (finding 4.1)

**Accessibility (High):**
- [x] Add `role="status" aria-live="polite"` to `#toast-container` and `role="feed"` to `#chronicle-feed` (finding 8.1)
- [x] On modal open, focus first focusable element; on close, return focus to trigger (finding 8.2 — via `focusModal()`)
- [x] Add visually-hidden skip-to-content link as first focusable element (finding 8.3)
- [x] Darken inactive bottom-nav tab from `text-slate-400` to `text-slate-500` for WCAG AA compliance (finding 8.4)

**Profile & Family (High):**
- [x] Replace `alert()` with styled `showToast()` in family management forms (finding P2)
- [x] Add "Leave family" button for non-owner family members (finding P3)

### 13b — Medium-Severity UX Fixes

**Layout & Visual:**
- [x] Add dark mode via `prefers-color-scheme: dark` CSS media query + `.dark-mode` class toggle (finding 1.2)
- [x] Expand content width to `max-w-2xl` on viewports >768px (findings 2.1, 7.3)
- [x] Add sliding underline indicator for active bottom-nav tab (finding 2.2)
- [x] Cap visible toasts at 3 with push-up stacking (finding 1.6)
- [x] Reduce confetti particle count to 25 (finding 1.4)
- [x] Audit all inputs for consistent `focus:ring-2 focus:ring-indigo-300 outline-none` styling (finding 5.3 — already present in most cases)

**Navigation & Interaction:**
- [x] Move family management to collapsible sections or sub-page on Profile (finding 2.3)
- [x] Show "My Family" badge or quick-link on Home tab (finding 4.4)
- [x] Add lightweight onboarding coachmark overlay (check `localStorage` flag — finding 4.5)
- [x] Add Escape key listener to close any open modal (finding 7.1)
- [x] Add styled confirmation modal (`showConfirmModal()` replacing `confirm()`) for logout (finding 11.4)
- [x] Handle session expiry gracefully: show toast + redirect to auth instead of instant reload (finding 11.3)
- [x] Show pending offline queue count (`#pending-indicator` in top-right) when non-empty (finding 10.6)

**Mobile UX:**
- [x] Listen to `window.visualViewport` resize events to scroll active input into view in modals (finding 6.5)
- [x] Increase minimum touch-target size on dynamic wizard rows to 44pt (findings 6.6, W4)
- [x] Make challenge detail slide in as a panel over the quest list instead of inline (finding 4.2)
- [x] Add `navigator.vibrate(10)` on successful log/redeem events (finding 5.6)

**Desktop UX:**
- [x] Add `hover:shadow-md hover:-translate-y-0.5 transition-all` to challenge cards and Up Next cards (finding 7.2)
- [x] Offer "download QR as PNG" option as alternative to popup print (finding 7.4)

**Scanner & Forms:**
- [x] Add "Enter code manually" fallback below scanner view (finding S2)
- [x] Increase bottom-nav label size from `text-[10px]` to `text-xs` (finding 3.1)

### 13c — Offline-First Architecture

**Local Persistence (IndexedDB):**
- [x] Define IndexedDB schema: `cache` and `pending` object stores
- [x] Write data access layer: `dbGet()`, `dbPut()`, `dbDelete()`, `dbGetAll()`, `dbClear()` — promise-based wrappers
- [x] Replace `localStorage` cache with IndexedDB for API response caching (GET requests cached to IndexedDB on success)
- [x] Seed IndexedDB from API responses on every successful GET fetch; fall back to IndexedDB on network failure

**Local-First Writes:**
- [x] On non-GET requests (activity log, challenge create/edit, prize redeem): write to IndexedDB `pending` store **before** network attempt, then try backend
- [x] On network failure: entry stays in `pending` store with status `pending`; no data loss
- [ ] Show optimistically updated UI from local data before backend confirms

**Background Sync & Queue:**
- [x] Replace localStorage offline queue with IndexedDB-backed `pending` store
- [ ] Register `SyncManager` event via service worker (`self.registration.sync.register('sync-pending')`) when supported
- [x] On `online` event: replay `pending` entries in order, remove on success, keep on failure
- [x] Show "Sync Complete" toast with count; "Sync Failed" toast for items that couldn't sync

**Pending State UI:**
- [x] Show "📤 N pending" badge (`#pending-indicator`) in top-right corner when `pending` store is non-empty
- [ ] Show pending entries in activity log / chronicle with a "sending..." or "pending" indicator
- [ ] Allow user to tap a pending entry to retry or discard

**Offline Read Capability:**
- [x] Service worker caches API `GET` responses with network-first + cache-fallback strategy for all `/api/*` routes (sw.js v4)
- [ ] Cache a minimal `offline.html` as fallback for uncached navigation (finding 10.5)
- [x] Read views fall back to IndexedDB cached data when network fails

**Service Worker & PWA Improvements:**
- [ ] Lazy-load `jsQR` only when scanner is opened (dynamic `import()` — finding 9.2)
- [x] Add themed splash screen via manifest `background_color: "#4f46e5"` (finding 10.2)
- [x] Add manifest `shortcuts` for common actions (New Challenge, Scan QR — finding 10.3)
- [x] Reconcile `./` vs `./index.html` double-caching: SW serves `./` for both routes (finding 10.1)
- [x] Add request deduplication (`dedupFetch()`) for parallel identical GETs (finding 9.4)
- [x] Add `lang="en"` to QR print template (finding 8.7)
- [x] Remove `pointer-events-none` from toast container (finding 8.5)

**Conflict Resolution:**
- [ ] Add `UpdatedAt` timestamp tracking on client-side pending entries
- [ ] On sync, API returns `409 Conflict` if server data is newer; client shows merge prompt or overwrites with server version
- [ ] Simple strategy: server wins for conflicting edits; local wins for new entries (no offline edits to existing entries for now)

### 13d — Remaining Low-Severity Polish

- [x] Add custom Tailwind theme config with named brand palette (finding 1.1 — `tailwind.config` block in index.html)
- [x] Constrain challenge descriptions with `max-w-prose` (finding 3.3)
- [x] Add hover state differentiation on challenge cards (finding 7.2 — `hover:shadow-md hover:-translate-y-0.5 transition-all`)
- [x] Replace `confirm()` for goal removal with styled modal (finding 5.5 — via `showConfirmModal()`)
- [ ] Add 3-second "undo" toast after activity log (finding 5.7)
- [x] Show "Updated Xm ago" instead of "Live" on stale chronicle items (finding H4 — switches after 24h)
- [x] Add step counter label "Step 2 of 4" in wizard (finding W1)
- [x] Make "Hidden" goal checkbox more visible (finding W3 — purple bg with badge)
- [x] Truncate goal descriptions in prize linked-goal dropdown (finding W5 — 35 char limit)
- [x] Add sort to achievements list on Profile (finding P1 — newest first)
- [x] Improve email validation regex (finding 5.2 — `/^[^\s@]+@[^\s@]+\.[^\s@]+$/`)
- [x] Add basic password strength indicator on register (finding A2)
- [x] Add smooth fade/slide transition on auth form toggle (finding A4 — `animate-fade-in`)
- [x] Show tooltip explaining 🍦 = total across all challenge currencies (finding H1 — `title` attribute dynamically set)
- [ ] Consider using `createElement` + `textContent` instead of `innerHTML` for user data (finding 8.6)

### 13e — Backdated Entries & Extended Log Editing

**Backend:**
- [x] `LogProgressRequest` already had `ClientRecordedAt` (renamed to `OccurredAt`) — when provided, used instead of `DateTime.UtcNow` for entry timestamp
- [x] `OccurredAt` added to `UpdateActivityDto` via same `LogProgressRequest` — edit endpoint already reuses this DTO
- [x] Streak recalculation: based on `now.Date` where `now = request.OccurredAt ?? DateTime.UtcNow` — streak logic already accounts for backdated dates
- [x] Chronicle feed sorts by `RecordedAt` (the backdated time) — correct chronological display
- [x] `ActivityLogEntryDto` and `ChronicleEntryDto` now include `CreatedAt` to distinguish logged time from recorded time
- [x] Pass `OccurredAt` through offline queue: serialized in request body as `occurredAt`

**Frontend:**
- [x] Add `<input type="date">` to Quick Log forms, challenge detail activity forms, and edit entry forms — defaults to today, max set to today (no future dates)
- [x] Add date picker to edit entry form — allows changing `OccurredAt` on existing entries
- [x] Show entry date in activity log entries; if backdated (`CreatedAt` differs from `RecordedAt` by >24h), show date tooltip "Logged X ago"
- [x] Show date instead of relative time for backdated items in chronicle feed
- [x] Disallow future dates via `max` attribute on all date pickers
- [x] For backdated entries in offline-first (13c): `occurredAt` included in IndexedDB `pending` entry body

## Phase 14 — Metric Categories

### 14a — MetricCategory Model & Backend (complete)

- [x] Add `ChallengeGoal.MetricCategory` field (string: "Distance", "Time", "Count")
- [x] DTOs: `CreateGoalDto.MetricCategory`, `UpdateGoalDto.MetricCategory`, `ChallengeGoalDto.MetricCategory`, `GoalProgressDto.MetricCategory`
- [x] `ChallengeService.cs` maps MetricCategory on create/update
- [x] `GoalService.cs`: metric-aware delta calculation in `LogActivityAsync` and `UpdateActivityEntryAsync`
  - Distance activities → Distance goals
  - Time activities → Time goals
  - Occurrence activities → Count goals
  - DistanceAndTime: Amount→Distance goals, TimeAmount→Time goals
  - Incompatible activity/goal pairs: skip goal progress update (activity still logged, currency/streak unaffected)

### 14b — Migration & Data Correction (complete)

- [x] EF migration `AddMetricCategoryToGoals`
- [x] Startup backfill: infer MetricCategory from goal unit (km/mi→Distance, min/hr→Time), fall back to linked activities
- [x] Startup recalculation: recompute all `GoalProgress.CurrentValue` using metric-aware delta, correct completion status
- [x] Backfill runs idempotently — only changes goals where current MetricCategory differs from inferred value

### 14c — Frontend UI (complete)

- [x] MetricCategory dropdown in wizard Step 2 advanced section (Distance, Time, Count)
- [x] Goal creation/update request includes `metricCategory`
- [x] MetricCategory badge on progress goal cards (cyan badge for non-Count categories)

### 14d — Remaining Polish

- [ ] Consider auto-deriving MetricCategory from linked activities when creating goal through wizard (pre-select based on first linked activity's type)
- [ ] Add unit-based MetricCategory inference to goal creation (if user enters "km" as unit, auto-select "Distance")
- [ ] Validate challenge creation: warn if no activity linked to a goal has a compatible MetricCategory