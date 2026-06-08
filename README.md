# glasstrut

Simple family challenge PWA. HTMX frontend (GitHub Pages) + .NET backend (local).

## Getting started

```sh
cd backend/Glasstrut.Api
dotnet build
dotnet run
```

Open `http://localhost:5088` in a browser. The backend serves the frontend in development mode and listens on the URLs shown in the terminal output.

### Configuration

SQLite database is created and migrated automatically on first run. Connection string is in `appsettings.json`. The JWT signing key is configured under the `Jwt` section.

### Tests

```sh
cd backend
dotnet test
```
