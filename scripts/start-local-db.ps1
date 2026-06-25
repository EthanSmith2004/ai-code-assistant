#!/usr/bin/env pwsh
# Starts the local CodeSight MySQL instance (created for local development).
#
# This points at a private data directory under %LOCALAPPDATA% so it does not
# collide with any other MySQL install. It does NOT require admin rights.
#
# DB the app connects to (see AiCodeAssistant.API/appsettings.Development.json):
#   server=127.0.0.1;port=3306;database=codesight;user=codesight;password=codesight
#
# Want it to auto-start on boot instead of running this script? Install it as a
# Windows service once, from an *admin* PowerShell:
#   & "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqld.exe" --install CodeSightMySQL --datadir="$env:LOCALAPPDATA\CodeSightMySQL\data"
#   Start-Service CodeSightMySQL

$ErrorActionPreference = 'Stop'

$mysqld  = 'C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqld.exe'
$dataDir = Join-Path $env:LOCALAPPDATA 'CodeSightMySQL\data'
$errLog  = Join-Path $env:LOCALAPPDATA 'CodeSightMySQL\mysql-error.log'

if (-not (Test-Path $mysqld))  { throw "mysqld.exe not found at $mysqld" }
if (-not (Test-Path $dataDir)) { throw "Data dir not found at $dataDir. Has the instance been initialized?" }

if (Get-CimInstance Win32_Process -Filter "Name='mysqld.exe'" -ErrorAction SilentlyContinue) {
    Write-Host "MySQL is already running." -ForegroundColor Green
    return
}

Start-Process -FilePath $mysqld `
    -ArgumentList "--datadir=`"$dataDir`"", "--port=3306", "--log-error=`"$errLog`"" `
    -WindowStyle Hidden

foreach ($i in 1..30) {
    Start-Sleep -Milliseconds 700
    & 'C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqladmin.exe' --host=127.0.0.1 --port=3306 --user=root --password=codesight ping 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) { Write-Host "MySQL is UP on 127.0.0.1:3306" -ForegroundColor Green; return }
}

Write-Warning "MySQL did not respond in time. Check the log: $errLog"
