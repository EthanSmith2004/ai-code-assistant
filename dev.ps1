#!/usr/bin/env pwsh
param()

$root = $PSScriptRoot
if (-not $root) { $root = Split-Path $MyInvocation.MyCommand.Path -Parent }

$queue     = [System.Collections.Concurrent.ConcurrentQueue[object]]::new()
$processes = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$events    = [System.Collections.Generic.List[System.Management.Automation.PSEventJob]]::new()

function Start-Service {
    param([string]$Project, [string]$Label, [string]$Color)

    $psi = [System.Diagnostics.ProcessStartInfo]::new("dotnet", "run --project $Project")
    $psi.WorkingDirectory       = $root
    $psi.UseShellExecute        = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true

    $proc = [System.Diagnostics.Process]::new()
    $proc.StartInfo        = $psi
    $proc.EnableRaisingEvents = $true

    $md = @{ Queue = $queue; Label = $Label; Color = $Color }

    foreach ($eventName in "OutputDataReceived", "ErrorDataReceived") {
        $script:events.Add(
            (Register-ObjectEvent -InputObject $proc -EventName $eventName -MessageData $md -Action {
                if ($null -ne $EventArgs.Data) {
                    $Event.MessageData.Queue.Enqueue(@{
                        Text  = "[$($Event.MessageData.Label)] $($EventArgs.Data)"
                        Color = $Event.MessageData.Color
                    })
                }
            })
        )
    }

    $null = $proc.Start()
    $proc.BeginOutputReadLine()
    $proc.BeginErrorReadLine()
    $script:processes.Add($proc)
}

function Stop-All {
    foreach ($proc in $script:processes) {
        if (-not $proc.HasExited) {
            taskkill /PID $proc.Id /T /F | Out-Null
        }
    }
    Get-EventSubscriber -ErrorAction SilentlyContinue | Unregister-Event -ErrorAction SilentlyContinue
    Get-Job             -ErrorAction SilentlyContinue | Remove-Job -Force -ErrorAction SilentlyContinue
}

try {
    Write-Host ""
    Write-Host "Starting services..." -ForegroundColor Cyan

    Start-Service -Project "AiCodeAssistant.API"    -Label "API   " -Color "Green"
    Start-Service -Project "AiCodeAssistant.Client" -Label "Client" -Color "Yellow"

    Write-Host "  API    -> http://localhost:5217" -ForegroundColor Green
    Write-Host "  Client -> http://localhost:5186" -ForegroundColor Yellow
    Write-Host "  Ctrl+C to stop both"             -ForegroundColor DarkGray
    Write-Host ""

    while ($true) {
        $item = $null
        while ($queue.TryDequeue([ref]$item)) {
            Write-Host $item.Text -ForegroundColor $item.Color
        }

        if (-not ($processes | Where-Object { -not $_.HasExited })) { break }

        Start-Sleep -Milliseconds 50
    }
}
finally {
    Write-Host ""
    Write-Host "Stopping..." -ForegroundColor DarkGray
    Stop-All
}
