# glasstrut

Simple family challenge PWA. HTMX frontend (GitHub Pages) + .NET backend (local).

## Stack

- **Frontend:** HTMX, plain CSS or Tailwind, static site on GitHub Pages
- **Backend:** .NET Web API, runs locally
- **Auth:** Email + password
- **PWA:** Yes (service worker + manifest)

## Requirements

- Users register/login with email + password
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
frontend/                   — HTMX static site (published to GitHub Pages)
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

Phase 1 (Foundation) complete. Phase 2 (Authentication) complete. Phase 3 (Families) complete. Phase 4 (Challenges) complete. Phase 5 (Goals & Achievements) complete. Phase 6 (Progress & Dashboard) mostly complete. Phase 7 (QR Reward Redemption) complete.

- QR redemption: server-side generation with QRCoder, token-based URLs
- `POST /api/challenges/{id}/prizes/{prizeId}/generate-redemption` — creates token + returns QR URL
- `GET /api/redeem/{token}/qr` — returns QR code PNG image (unauthenticated, for printing)
- `POST /api/redeem/{token}` — redeems prize, deducts points from currency goal balance
- Frontend: modal with printable reward coupon card (large QR + prize details, print button)

- Activities as first-class concepts: each goal has one or more activities (named modes of achieving, e.g. "Running" → "km", "Mow lawn" → "times")
- Goal types: Achievement (target-based) or Currency (point accumulation via activities)
- Activity-based progress logging: log an activity with amount, system calculates `delta = amount * activity.PointValue`
- Goal progress auto-completes when `CurrentValue >= TargetValue`; achievements auto-awarded on completion
- Multi-user progress view: family challenges show tabs per member with individual progress bars
- Activity log: `GET /api/challenges/{id}/activity-log` returns recent entries with user, activity, amount, notes
- Dashboard stats row shows active challenge count and goal completion summary
- Challenge cards show completion summary + SelfOnly/Family badge + currency badge
