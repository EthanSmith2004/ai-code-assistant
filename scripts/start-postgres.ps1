#!/usr/bin/env pwsh
# Starts the local CodeSight PostgreSQL instance (portable, no admin required).
#
# A portable PostgreSQL 16 was extracted to %LOCALAPPDATA%\CodeSightPostgres.
# The app connects with (see AiCodeAssistant.API/appsettings.Development.json):
#   Host=127.0.0.1;Port=5432;Database=codesight;Username=postgres;Password=postgres
#
# The cluster uses trust auth on localhost, so the password is not actually
# checked. EF Core applies migrations (and creates the 'codesight' database) on
# API startup.

$ErrorActionPreference = 'Stop'

$pgHome = Join-Path $env:LOCALAPPDATA 'CodeSightPostgres'
$bin    = Join-Path $pgHome 'pgsql\bin'
$data   = Join-Path $pgHome 'data'
$log    = Join-Path $pgHome 'pg.log'

if (-not (Test-Path "$bin\postgres.exe")) { throw "postgres.exe not found at $bin" }
if (-not (Test-Path $data))               { throw "Data dir not found at $data (cluster not initialized)." }

if (Get-Process -Name postgres -ErrorAction SilentlyContinue) {
    Write-Host "PostgreSQL is already running." -ForegroundColor Green
    return
}

# Start detached so it survives this shell session.
Start-Process -FilePath "$bin\postgres.exe" -ArgumentList "-D", "`"$data`"", "-p", "5432" -WindowStyle Hidden

foreach ($i in 1..30) {
    Start-Sleep -Milliseconds 600
    if ((Test-NetConnection -ComputerName 127.0.0.1 -Port 5432 -WarningAction SilentlyContinue).TcpTestSucceeded) {
        Write-Host "PostgreSQL is UP on 127.0.0.1:5432" -ForegroundColor Green
        return
    }
}

Write-Warning "PostgreSQL did not respond in time. Check the log: $log"
