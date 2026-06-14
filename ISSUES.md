# Issues

This document tracks known issues, bugs, and improvements for the Glasstrut project.

## Resolved

### #001 - CurrencyEarned zero guard — skip balance creation when no points earned
- **Status:** Fixed
- **Component:** Backend
- **Details:** When `activity.PointValue` is 0, `amount × 0 = 0`, which would create a `ChallengeCurrencyBalance` with balance 0 and streak 1. Fixed by only setting `currencyEarned` when `raw > 0`.

### #002 - T034 — Hidden goal surprise title assertion (test bug)
- **Status:** Fixed
- **Component:** Tests
- **Details:** Assertion used `Assert.Contains("Surprise", ...)` but the returned title was `"Hidden goal completed: Hidden surprise"` (lowercase 's'). Fixed with `StringComparison.OrdinalIgnoreCase`.

### #003 - Prize linked to goal via create with null challengeGoalId
- **Status:** Fixed
- **Component:** Tests
- **Details:** `CreatePrizeDto.challengeGoalId` was `null` at creation time because goal GUIDs don't exist yet. The test now links the prize to the goal via `PUT /api/challenges/{id}` before attempting redemption.

### #004 - JWT base64url decoding in tests
- **Status:** Fixed
- **Component:** Tests
- **Details:** JWT uses base64url encoding. The manual token decoding in the test did not account for this. Fixed by replacing base64url chars before decoding.

### #009 - Multiple SaveChangesAsync calls cause partial saves on failure in LogActivityAsync
- **Status:** Fixed
- **Component:** Backend
- **Details:** `AutoAwardAchievementAsync` and `AutoClaimPrizeAsync` called `SaveChangesAsync` independently. Refactored to register changes only (no save), letting the caller control a single `SaveChangesAsync` at the end.

### #010 - Hidden goal early return skips currency balance update
- **Status:** Fixed
- **Component:** Backend
- **Details:** Currency balance upsert moved before the hidden goal early return path, ensuring balance/streak are updated before returning with the SurpriseDto.

### #011 - SelfOnly challenge access checks missing across multiple endpoints
- **Status:** Fixed
- **Component:** Backend
- **Details:** `RedeemService.VerifyAccessAsync` now checks `CreatedById` for SelfOnly challenges. `GoalService.GetChallengeProgressAsync` now has both family membership and SelfOnly ownership checks.

### #017 - No validation of negative or zero Amount in LogActivityAsync
- **Status:** Fixed
- **Component:** Backend
- **Details:** Added `if (request.Amount <= 0)` guard at the top of `LogActivityAsync`.

### #021 - ProgressEntry not linked to GoalProgress for existing progress records
- **Status:** Fixed
- **Component:** Backend
- **Details:** Added `progress.ProgressEntries.Add(entry)` in the existing-progress branch (was only adding on new progress creation).

### #024 - AutoAwardAchievementAsync: first achievement gets wrong IsHidden value
- **Status:** Fixed
- **Component:** Backend
- **Details:** Changed hardcoded `IsHidden = false` to `goal.IsHidden` when creating a new achievement.

### #030 - apiFetch sets wrong default Content-Type for all requests
- **Status:** Fixed
- **Component:** Frontend
- **Details:** Changed to only set `Content-Type: application/x-www-form-urlencoded` for non-JSON, non-FormData payloads.

### #031 - JWT atob() fails on base64url-encoded payloads
- **Status:** Fixed
- **Component:** Frontend
- **Details:** Added `base64UrlDecode()` function (`-` → `+`, `_` → `/`, pad with `=`) and replaced all `atob(...)` calls on JWT payloads.

### #039 - renderAchievements() called without null check
- **Status:** Fixed
- **Component:** Frontend
- **Details:** Added `!achievements ||` guard before `.length` check.

### #032 - Variable shadowing of editId in submitChallenge()
- **Status:** Fixed
- **Component:** Frontend
- **Details:** Renamed inner `const editId` to `const goalEditId` in the goal-field loop to eliminate confusion.

### #034 - Null email crashes in multiple rendering functions
- **Status:** Fixed
- **Component:** Frontend
- **Details:** Guarded `e.userEmail.split('@')[0]` and `e.userEmail[0]` with fallback to "unknown".

### #042 - Multiple currency names across challenges — points display conflates them
- **Status:** Fixed
- **Component:** Frontend
- **Details:** `refreshPoints()` now uses per-challenge currency names via a currencies dictionary and only shows the symbol when there's exactly one unique currency name.

### #043 - Challenge edit modal currency name field could be clearer
- **Status:** Not relevant (already a placeholder change)

### #005 - GoalProgress to ProgressEntry relationship not configured in DbContext
- **Status:** Fixed (via shadow FK in FixFKAndAddIndexes migration)
- **Component:** Backend
- **Details:** The `GoalProgress.ProgressEntries` navigation creates a shadow FK `GoalProgressId` column in the ProgressEntries table, which is already present in the FixFKAndAddIndexes migration.

### #012 - GoalService.LogActivityAsync does not verify user is a member of Targeted challenges
- **Status:** Fixed
- **Component:** Backend
- **Details:** Added `ChallengeTargets` check in `LogActivityAsync` for "Targeted" type challenges.

### #015 - UpdateChallengeAsync missing Include for Activities collection
- **Status:** Fixed
- **Component:** Backend
- **Details:** Added `.Include(c => c.Activities)` to the `UpdateChallengeAsync` query.

### #016 - AutoAwardAchievementAsync uses wrong deduplication logic
- **Status:** Fixed
- **Component:** Backend
- **Details:** Changed to create per-goal achievements with `ChallengeGoalId` FK. Deduplication now checks `AchievementId` + `UserId` instead of per-challenge.

### #018 - Prize ChallengeGoalId not validated against challenge's own goals
- **Status:** Fixed
- **Component:** Backend
- **Details:** Added validation in both `AddGoalsAndPrizes` and `MergeGoalsAndPrizesAsync` that checks `ChallengeGoalId` against the challenge's own goal IDs.

### #019 - AuthEndpoints & FamilyEndpoints: NullReferenceException on missing form fields
- **Status:** Fixed
- **Component:** Backend
- **Details:** Replaced `form["field"]!` with `form["field"].FirstOrDefault()` + null/empty checks, returning BadRequest with a descriptive message.

### #020 - Validation missing for Challenge Type, ActivityType, Goal Type, and EndDate
- **Status:** Fixed
- **Component:** Backend
- **Details:** Added allowed-types check for challenge Type in `CreateChallengeAsync`. Added `EndDate > StartDate` validation in both create and update.

### #033 - loadChallenges() and other endpoints bypass apiFetch — no 401 auto-redirect
- **Status:** Fixed
- **Component:** Frontend
- **Details:** All `fetch()` calls converted to use `apiFetch()`, ensuring centralized 401 auto-logout logic applies everywhere.

### #035 - Missing error handling for res.json() on non-OK responses
- **Status:** Fixed
- **Component:** Frontend
- **Details:** Added `.catch(() => ({}))` fallback for `res.json()` calls in error-handling branches.

### #038 - DOMContentLoaded doesn't call showAuth() when no token exists
- **Status:** Fixed
- **Component:** Frontend
- **Details:** Added `else { showAuth(); }` block when no token is found on load.

## Open

### #006 - ChallengeActivity has two cascade delete paths from Challenge
- **Severity:** Medium
- **File:** `AppDbContext.cs`
- **Details:** `ChallengeActivity` cascades from both `Challenge` and `ChallengeGoal`. Deleting a Challenge creates two cascade paths.

### #007 - PrizeClaim has two cascade delete paths from Challenge
- **Severity:** Medium
- **File:** `AppDbContext.cs`
- **Details:** `PrizeClaim` cascades from both `Prize` and `Challenge`. Deleting a Challenge creates two cascade paths.

### #008 - Token expiration too long (7 days) with no refresh mechanism
- **Severity:** Low
- **File:** `AuthService.cs`
- **Details:** JWT tokens expire after 7 days with no refresh token mechanism.

### #013 - Duplicate PrizeClaim race condition in RedeemPrizeAsync
- **Severity:** Medium
- **File:** `RedeemService.cs`
- **Details:** Check-then-act pattern (`AnyAsync`) is vulnerable to concurrent requests. Unique index added as defense-in-depth.

### #014 - Currency balance race condition in LogActivityAsync
- **Severity:** Medium
- **File:** `GoalService.cs`
- **Details:** Check-then-act pattern for `ChallengeCurrencyBalance`. Concurrent activity logs could cause `DbUpdateException` or lost updates.

### #022 - Prize cost with no currency name — deduction skipped silently
- **Severity:** Medium
- **File:** `RedeemService.cs`
- **Details:** Cost-deduction only runs when `challenge.CurrencyName` is non-null, so prizes with cost > 0 for currency-less challenges bypass deduction.

### #023 - DTOs use Email instead of Username
- **Severity:** Low
- **File:** `FamilyDtos.cs`, `GoalDtos.cs`, `RedemptionDtos.cs`
- **Details:** DTOs expose `Email` instead of `UserName` (Phase 9).

### #025 - Unvalidated count parameter in GetActivityLogAsync
- **Severity:** Low
- **File:** `GoalEndpoints.cs`, `GoalService.cs`
- **Details:** `count` parameter has no upper bound.

### #026 - Streak counter depends on server UTC date — not user-local time
- **Severity:** Low
- **File:** `GoalService.cs`
- **Details:** Uses `DateTime.UtcNow.Date`. Users in different timezones may get streaks incorrectly reset.

### #027 - Claim history shows all claims for a challenge, not filtered to user
- **Severity:** Low
- **File:** `RedeemService.cs`
- **Details:** Returns claims for all users, potentially leaking redemption info.

### #028 - Challenge type change not supported but not blocked
- **Severity:** Low
- **File:** `ChallengeService.cs`
- **Details:** `Type`/`FamilyId` change requests silently ignored.

### #029 - FamilyEndpoints CreateFamily and JoinFamily return 200 instead of 201
- **Severity:** Low
- **File:** `FamilyEndpoints.cs`
- **Details:** Should return 201 Created.

### #036 - Service worker caching strategy issues
- **Severity:** Medium
- **File:** `frontend/sw.js`
- **Details:** Cache-first for CDN without versioning, precache missing CDN deps, hardcoded `CACHE_NAME`.

### #037 - renderChronicleFeed() and loadClaims() fetch sequentially
- **Severity:** Medium
- **File:** `frontend/js/app.js`
- **Details:** API calls use `for...of` + `await` instead of `Promise.all()`.

### #040 - printQr() uses blob URL which is origin-bound
- **Severity:** Medium
- **File:** `frontend/js/app.js`
- **Details:** `blob:http://...` URL in new window may not load properly for `window.print()`.
