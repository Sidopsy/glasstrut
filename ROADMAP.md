# Roadmap

## Phase 1 — Foundation
- [x] Initialize frontend (HTMX static site skeleton)
- [x] Initialize .NET Web API backend
- [x] Set up GitHub Pages deployment for frontend
- [x] Set up PWA manifest + service worker
- [x] Set up database schema (SQLite / local DB)

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
- [x] Multiple goals per challenge ("prime directives")
- [x] Multiple prizes per challenge
- [x] Activities as modes of achieving goals (running, mowing, etc.)
- [x] Goal types: Achievement (target-based) or Currency (point accumulation)
- [x] Activity-based progress logging (log what you did, system calculates progress)

## Phase 5 — Goals & Achievements
- [x] Goal completion tracking
- [x] Achievement system
- [x] Achievement display

## Phase 6 — Progress & Dashboard (complete)
- [x] Challenge progress views (member tabs for family challenges, activity history log)
- [x] User dashboard (stats row, challenge summaries, achievement counts)
- [x] Activity log endpoint + frontend display (+ `renderActivityLog`, `renderAchievements`)
- [x] Multi-user progress endpoint (`GET /api/challenges/{id}/progress/members`)
- [ ] Notifications (optional)

## Phase 7 — QR Reward Redemption (complete)
- [x] QR code generation for prizes (server-side with QRCoder)
- [x] Static QR encodes challenge+prize IDs (`{baseUrl}/?claim=challengeId:prizeId`)
- [x] Point deduction + prize fulfillment (deducts from first Currency goal's progress)
- [x] Printable reward coupon view (opens print dialog with styled layout)
- [x] Redemption history per challenge (`GET /api/challenges/{id}/claims`)

## Phase 8 — Polish
- [ ] Styling (Tailwind / CSS)
- [ ] Offline support (PWA caching)
- [ ] Testing
- [ ] Documentation
