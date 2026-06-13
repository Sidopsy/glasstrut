# glasstrut

Simple family challenge PWA. HTMX frontend (GitHub Pages) + .NET backend (local).

## Stack

- **Frontend:** Vanilla JS + Tailwind CSS (CDN), static site on GitHub Pages
- **Backend:** .NET 10 Web API (minimal API, service + repo layers), SQLite
- **Auth:** Email + password via ASP.NET Core Identity + JWT
- **PWA:** Yes (service worker + manifest)

## Requirements

- Users register/login with email + password (username planned)
- Users create/join families
- Challenges can be family-wide, self-only, or targeted at specific members
- Challenges have optional time constraints, one or many goals, one or many prizes
- Goals generate achievements
- Users can view challenge progress

## Workflow

- Follow [ROADMAP.md](./ROADMAP.md) and check off items as they're completed.
- Keep this file and [README.md](./README.md) up to date throughout development — document commands, conventions, and quirks as they emerge.
- At big architectural intersections, stop and ask the user for direction before proceeding.
- Feel free to refactor/remove content in README.md that no longer reflects the current state of the project.
- After completing a tested, working step, create a commit for those changes.
- After a phase is complete, tested, and signed off, push all commits for that phase.
- No pull requests — commit and push directly to main. Use only safe git commands: `status`, `add`, `commit`, `push`. Never force push.

## Layout

```
backend/Glasstrut.Api/      — .NET 10 Web API (minimal API, service + repo layers)
backend/Glasstrut.Api.Tests/ — xUnit integration tests
frontend/                   — Vanilla JS + Tailwind static site (published to GitHub Pages)
.github/workflows/          — CI / deployment
```

## Commands

```sh
# Backend — build & run
cd backend/Glasstrut.Api
dotnet build
dotnet run

# EF Core migrations (after adding/changing models)
dotnet ef migrations add <Name>
# Migrations auto-apply on startup in development — no manual update needed

# Tests
cd backend
dotnet test
```

## Conventions

- Keep model, service, repository, and endpoint files in their respective directories under `backend/Glasstrut.Api/`.
- Use `DateTime.UtcNow` for timestamps.
- Backend listens locally; frontend calls it via API.
- GitHub Pages deploys the `frontend/` directory automatically on pushes to `main` that touch `frontend/**`.
- Never commit secrets or keys to git. Never expose or log secrets in code.

## Current status

Phase 1–5 complete (Foundation, Auth, Families, Challenges, Goals & Achievements).
Phase 6–7 complete (Progress & Dashboard, QR Reward Redemption).
Phase 8 UI redesign + challenge enhancements done.
Phase 9 Username Auth complete.
Phase 10 Currency Overhaul & Streak Counter complete.

### Phase 8 — UI Redesign & Challenge Enhancements (complete)
- Tailwind CSS via CDN replaces most custom CSS
- 4-tab layout with bottom navigation: Home, Quests, Treasury, Profile
- Auth view redesigned: single form with login/register toggle, emoji-labeled inputs, email validation
- Home tab: day-based greeting header, points display, horizontal "Up Next" challenge cards, "Family Chronicles" activity feed
- Quests tab: challenge list with create button (modal), detail view with activity logging
- Treasury tab: QR scanner button, prize listing grouped by challenge, claim history
- Profile tab: achievements wall, family management, logout
- In-app QR scanner: camera via `getUserMedia`, frame decode with `jsQR`, auto-redemption flow
- Challenge model enhancements:
  - `ChallengeGoal.IsHidden` (bool) — secret milestones
  - `ChallengePrize.HasQR` (bool) — QR generation toggle
  - `ChallengePrize.ChallengeGoalId` (Guid?) — FK linking prize to specific goal
  - `ChallengeActivity.ActivityType` (string: Distance/Time/DistanceAndTime/Occurrence)
  - `ProgressEntry.TimeAmount` (decimal?) — secondary value for DistanceAndTime activities
- GoalService: auto-claim hidden goal prizes, return SurpriseDto, filter hidden goals from non-completing viewers
- RedeemService: HasQR check, skip cost for goal-linked prizes, verify linked goal completion
- `PUT /api/challenges/{id}` endpoint with smart-merge update logic
- Create/edit challenge modal with dynamic goal/prize/activity rows
- DistanceAndTime dual-input forms in activity logging
- Toast notifications for hidden goal discoveries and prize redemptions
- Edit button on challenge cards (visible to creator only)

### Phase 9 — Username Auth (complete)
- `IdentityUser.UserName` now used as actual username (previously set to email)
- Login accepts username or email
- Registration accepts optional username (falls back to email prefix)
- Username displayed in frontend dashboard; avatar initial uses username
- JWT includes `Name` claim with username
- Duplicate username check on registration
- Full test coverage: 67 tests pass

### Phase 10 — Currency Overhaul & Streak Counter (complete)
- New `ChallengeCurrencyBalance` table stores per-user, per-challenge currency balance
- `ProgressEntry.CurrencyEarned` (decimal?) records per-activity points earned
- Currency earned = `amount × activity.PointValue` when challenge has `CurrencyName`
- `GoalService.LogActivityAsync` upserts balance, updates streak counter
  - Same-day activity: streak unchanged
  - Yesterday: streak increments
  - Earlier: streak resets to 1
- `RedeemService.RedeemPrizeAsync` uses `ChallengeCurrencyBalance.Balance` instead of summing Currency goal progress
- Currency goal type removed from create/edit modal (replaced by per-challenge currency)
- Frontend: balance + streak displayed in progress views, `currencyEarned` shown in activity log + toast
- `refreshPoints()` sums `p.currencyBalance` across challenges
- Migration: `AddCurrencyBalanceAndStreak`
- Full test coverage: 67 tests still pass
