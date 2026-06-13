# Issues

## Resolved

### CurrencyEarned zero guard — skip balance creation when no points earned
- **Status:** Fixed
- **File:** `backend/Glasstrut.Api/Services/GoalService.cs:40-44`
- **Details:** When `activity.PointValue` is 0, `amount × 0 = 0`, which would create a `ChallengeCurrencyBalance` with balance 0 and streak 1. Fixed by only setting `currencyEarned` when `raw > 0`.

### T034 — Hidden goal surprise title assertion (test bug)
- **Status:** Fixed
- **Test:** `T034_Goal_HiddenGoal_ReturnsSurprise`
- **Details:** Assertion used `Assert.Contains("Surprise", ...)` but the returned title was `"Hidden goal completed: Hidden surprise"` (lowercase 's'). Fixed with `StringComparison.OrdinalIgnoreCase`.

### T042 — Prize linked to goal via create with null challengeGoalId
- **Status:** Fixed
- **Test:** `T042_Prize_Redeem_GoalLinkedPrize_RequiresCompletion`
- **Details:** `CreatePrizeDto.challengeGoalId` was `null` at creation time because goal GUIDs don't exist yet. The test now links the prize to the goal via `PUT /api/challenges/{id}` before attempting redemption.
- **Same fix applied to:** `T043_Prize_Redeem_GoalLinked_AfterCompletion_Succeeds`

### JWT base64url decoding in tests
- **Status:** Fixed
- **Test:** `CreateTargetedChallenge`
- **Details:** JWT uses base64url encoding (`-` and `_` instead of `+` and `/`). The manual token decoding in the test did not account for this. Fixed by replacing base64url chars before decoding.

## Open

### Multiple currency names across challenges — points display conflates them
- **Severity:** Low
- **File:** `frontend/js/app.js:refreshPoints()`
- **Details:** `refreshPoints()` sums all `p.currencyBalance` values but uses the last challenge's `currencyName` for the display label. If a user has "Coins" (balance 5) and "Stars" (balance 3), the total shows as "8 Stars" instead of showing them separately. Defer fixing until multi-currency support is actually used.

### Prize cost with no currency name — deduction skipped silently
- **Severity:** Medium
- **File:** `backend/Glasstrut.Api/Services/RedeemService.cs:68`
- **Details:** The cost-deduction block only runs when `challenge.CurrencyName` is non-null. If a prize has `cost > 0` but the challenge has no `CurrencyName`, the cost is shown in the UI but never enforced. Existing challenges created before Phase 10 with Currency-type goals but no CurrencyName fall into this category. Mitigation: since the currency model is new, existing data can be reset. Long-term fix: make cost enforcement independent of CurrencyName, or require CurrencyName when any prize has a cost.

### Streak counter depends on server UTC date — not user-local time
- **Severity:** Low
- **File:** `backend/Glasstrut.Api/Services/GoalService.cs:99`
- **Details:** The streak counter uses `DateTime.UtcNow.Date` to determine "today". A user who logs activity at 11 PM their time (which might be UTC+2 → 1 AM next day UTC) could have their streak incorrectly reset because the server sees it as a new day. Fix would require sending the user's timezone offset or storing the client-reported date.

### Claim history shows all claims for a challenge, not filtered to user
- **Severity:** Low
- **File:** `backend/Glasstrut.Api/Services/RedeemService.cs:105-124`
- **Details:** `GetPrizeClaimsAsync` returns claims for *all* users in the challenge, not just the requesting user. The frontend displays this in the Treasury tab claim history. This is arguably a feature (family visibility of who's claiming what), but it leaks per-user redemption info. No fix planned unless policy changes.

### `GoalService.LogActivityAsync` does not verify user is a member of Targeted challenges
- **Severity:** Medium
- **File:** `backend/Glasstrut.Api/Services/GoalService.cs:29-35`
- **Details:** The auth check only handles "SelfOnly" vs. non-SelfOnly (family member check). For "Targeted" challenges, the check passes if the user is a family member — but it doesn't verify the user is specifically targeted. A non-targeted family member could log activities for a targeted-only challenge. Fix: add a check for `ChallengeTarget` entries when `challenge.Type == "Targeted"`.

### Challenge edit modal currency name field could be clearer
- **Severity:** Low
- **File:** `frontend/app.js` (challenge modal)
- **Details:** The currency name field at `index.html` is labeled "Currency name (optional)". It's not obvious that this enables per-activity point earning. Consider adding a help tooltip or changing the label to "Points currency name (e.g., Coins, Stars)" to clarify intent.
