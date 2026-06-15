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
