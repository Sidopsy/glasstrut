# glasstrut

Simple family challenge PWA. Vanilla JS + Tailwind CSS (CDN) frontend (GitHub Pages) + .NET 10 backend (local).

## Stack

- **Frontend:** Vanilla JS + Tailwind CSS (CDN), static site on GitHub Pages
- **Backend:** .NET 10 Web API (minimal API), SQLite, ASP.NET Core Identity + JWT
- **PWA:** Service worker + web app manifest
- **Proxy:** Caddy (HTTPS + reverse proxy to backend)

## Auth

Email + password via ASP.NET Core Identity. JWT stored in `localStorage`. Login accepts username or email.

## Local development

```sh
# Start the backend
cd backend/Glasstrut.Api
dotnet run
# → http://localhost:5088

# Open frontend
# Serve the frontend/ directory with any static file server, or open index.html directly.
# The frontend detects localhost and uses http://localhost:5088 automatically.
```

### Tests

```sh
cd backend
dotnet test
```

## Production deployment

You need a server that can reach both the internet and your backend. Pick one approach:

| Approach | Cost | URL | Setup |
|---|---|---|---|
| **DuckDNS + Caddy** (recommended) | Free | Permanent (`glasstrut.duckdns.org`) | 5 min DNS config |
| **ngrok** | Free tier (URL changes) / Paid (fixed) | Temporary (`abc123.ngrok.io`) | One command, no DNS |

Both give you HTTPS → your local backend.

### Option A — DuckDNS + Caddy

1. Go to [duckdns.org](https://duckdns.org), sign in with GitHub/Google, create a domain (e.g. `glasstrut.duckdns.org`), and set its A record to your server's public IP.

2. Install [Caddy](https://caddyserver.com) on the server. It auto-provisions Let's Encrypt TLS certs.

3. The `Caddyfile` in the repo root already points to `glasstrut.duckdns.org`. Run:

   ```sh
   caddy run
   ```

4. Deploy the backend:

   ```sh
   cd backend/Glasstrut.Api
   dotnet publish -c Release -o /opt/glasstrut/api

   export Jwt__Key="your-strong-256-bit-key-here-min-32-chars!!"
   export Cors__AllowedOrigins__0="https://your-org.github.io"

   dotnet /opt/glasstrut/api/Glasstrut.Api.dll --urls http://127.0.0.1:5088
   ```

   The backend listens on `127.0.0.1:5088` — only Caddy (same machine) reaches it.

### Option B — ngrok (no DNS, no Caddy)

```sh
ngrok http http://localhost:5088
# → https://abc123.ngrok.io
```

That's it — HTTPS is handled by ngrok. No Caddy needed.

### Frontend (GitHub Pages)

1. Edit `frontend/js/config.js` — replace the DuckDNS placeholder with your actual backend URL.
2. Commit and push to `main` → the GitHub Action auto-deploys to Pages.

The frontend auto-detects:
- **Local dev** (localhost / file://) → `http://localhost:5088`
- **Deployed** (any other host) → your backend URL

### Architecture

```
Browser at https://your-org.github.io
    │  HTTPS
    ▼
Caddy / ngrok at https://glasstrut.duckdns.org
    │  reverse proxy
    ▼
.NET backend on 127.0.0.1:5088
```

CORS is locked to your GH Pages origin when `Cors__AllowedOrigins__0` is set. In dev (empty array), all origins are allowed.

## Configuration reference

| Setting | Env var | Description |
|---|---|---|
| `Jwt:Key` | `Jwt__Key` | JWT signing key (min 32 chars). Change for production. |
| `Cors:AllowedOrigins:0` | `Cors__AllowedOrigins__0` | Allowed CORS origin (e.g., GitHub Pages URL). Leave empty for `AllowAnyOrigin` (dev). |
| `ConnectionStrings:DefaultConnection` | — | SQLite path. Default: `glasstrut.db` in the working directory. |

## Project layout

```
backend/Glasstrut.Api/       — .NET 10 Web API
backend/Glasstrut.Api.Tests/ — xUnit integration tests
frontend/                    — Vanilla JS + Tailwind static site
Caddyfile                    — Caddy reverse proxy config (DuckDNS)
.github/workflows/           — CI / deployment
```
