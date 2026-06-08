# glasstrut

Simple family challenge PWA. HTMX frontend (GitHub Pages) + .NET backend (local).

## Getting started

### Backend

```sh
cd backend/Glasstrut.Api
dotnet build
dotnet run
```

The API listens on the URLs shown in the terminal output (typically `http://localhost:5000` and `https://localhost:5001`).

### Frontend

Open `frontend/index.html` directly in a browser, or serve it locally:

```sh
# using npx
npx serve frontend

# or any other static file server
```

In production, the frontend is deployed to GitHub Pages automatically (see CI workflow).

### Configuration

The backend expects a SQLite database file (created automatically on first run). Connection string is in `appsettings.json`.
