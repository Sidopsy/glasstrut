# glasstrut

Simple family challenge PWA. Vanilla JS + Tailwind CSS (CDN) frontend (GitHub Pages) + .NET 10 backend (SQLite).

## Stack

- **Frontend:** Vanilla JS + Tailwind CSS (CDN), static site on GitHub Pages
- **Backend:** .NET 10 Web API (minimal API), SQLite, ASP.NET Core Identity + JWT
- **PWA:** Service worker + web app manifest

## Commands

```sh
# Backend
cd backend/Glasstrut.Api
dotnet build
dotnet run                # → http://localhost:5088

# Tests
cd backend
dotnet test

# Serve frontend locally
npx serve frontend        # or any static file server
```

## Layout

```
backend/Glasstrut.Api/       — .NET 10 Web API
backend/Glasstrut.Api.Tests/ — xUnit integration tests
frontend/                    — Vanilla JS + Tailwind static site
Caddyfile                    — Caddy reverse proxy config
.github/workflows/           — CI / deployment
```
