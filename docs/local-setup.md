# Local Development Setup

## Database (PostgreSQL)

Local dev uses a portable PostgreSQL 16 instance (no admin/install needed) with
its data directory under `%LOCALAPPDATA%\CodeSightPostgres\data`. The cluster
uses trust auth on localhost.

Connection string (in `AiCodeAssistant.API/appsettings.Development.json`):

```
Host=127.0.0.1;Port=5432;Database=codesight;Username=postgres;Password=postgres;SSL Mode=Disable;Trust Server Certificate=true
```

### Starting the database

```powershell
./scripts/start-postgres.ps1
```

This launches `postgres.exe` against the local data directory, detached so it
survives the shell session. It does **not** auto-start on reboot — run the script
again after a restart.

### Schema

The API applies EF Core migrations on startup (`Database.MigrateAsync()`), which
also creates the `codesight` database if it does not exist — so a fresh cluster
is provisioned automatically the first time the API runs.

> The previous local MySQL setup (`scripts/start-local-db.ps1`,
> `%LOCALAPPDATA%\CodeSightMySQL`) is no longer used.

## Demo codebases

The workspace's "Demo codebase" dropdown scans the bundled sample projects under
`samples/` (ASP.NET Core, Express, FastAPI, Gin, Spring Boot). The API resolves
that folder via `Samples:Path` in `appsettings.Development.json` (falling back to
`<contentRoot>/../samples`).

## Running the app

With the database running:

```powershell
./dev.ps1
```

- API    -> http://localhost:5217 (Swagger at `/swagger`)
- Client -> http://localhost:5186
