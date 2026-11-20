<#
.SYNOPSIS
    Starts the Employee Management API and the React frontend for local development.

.DESCRIPTION
    Runs the API (http://localhost:5000) and the Vite dev server (http://localhost:5173) as background jobs.
    If either port is already in use, the script shows which process is listening and stops only that
    process, and only when you confirm (or pass -Force). It never stops unrelated dotnet/node processes.

    Secrets (DB connection string, OpenRouter API key) must already be in .NET user-secrets. See README.md.

.PARAMETER Force
    Stop processes listening on the API/frontend ports without asking.

.EXAMPLE
    ./start-dev.ps1
    ./start-dev.ps1 -Force
#>
[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$ApiPort      = 5000
$FrontendPort = 5173
$ApiUrl       = "http://localhost:$ApiPort"
$FrontendUrl  = "http://localhost:$FrontendPort"

$RootDir     = $PSScriptRoot
$ApiProject  = Join-Path $RootDir 'EmployeeManagement/src/EmployeeManagement.Api/EmployeeManagement.Api.csproj'
$FrontendDir = Join-Path $RootDir 'EmployeeManagement-FE'

function Resolve-PortConflict {
    param([int]$Port)

    $listeners = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if (-not $listeners) { return $true }

    foreach ($processId in ($listeners.OwningProcess | Sort-Object -Unique)) {
        $proc = Get-Process -Id $processId -ErrorAction SilentlyContinue
        $name = if ($proc) { $proc.ProcessName } else { 'unknown' }
        Write-Host "Port $Port is in use by $name (PID $processId)." -ForegroundColor Yellow

        $stop = $Force
        if (-not $stop) {
            $answer = Read-Host "Stop $name (PID $processId)? [y/N]"
            $stop = $answer -match '^(y|yes)$'
        }

        if ($stop) {
            Stop-Process -Id $processId -Force
            Write-Host "Stopped PID $processId." -ForegroundColor Green
        }
        else {
            Write-Host "Leaving PID $processId running. Cannot start on port $Port." -ForegroundColor Red
            return $false
        }
    }
    return $true
}

Write-Host '========================================' -ForegroundColor Cyan
Write-Host '  Employee Management - dev environment ' -ForegroundColor Cyan
Write-Host '========================================' -ForegroundColor Cyan

if (-not (Resolve-PortConflict -Port $ApiPort))      { exit 1 }
if (-not (Resolve-PortConflict -Port $FrontendPort)) { exit 1 }

Write-Host "`nStarting API on $ApiUrl ..." -ForegroundColor Green
$apiJob = Start-Job -Name 'em-api' -ScriptBlock {
    param($project, $url)
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    dotnet run --project $project --launch-profile http --urls $url
} -ArgumentList $ApiProject, $ApiUrl

Write-Host "Starting frontend on $FrontendUrl ..." -ForegroundColor Green
$feJob = Start-Job -Name 'em-fe' -ScriptBlock {
    param($dir, $port)
    Set-Location $dir
    npm run dev -- --port $port --strictPort
} -ArgumentList $FrontendDir, $FrontendPort

Write-Host "`nAccess points:" -ForegroundColor Cyan
Write-Host "  API:      $ApiUrl"
Write-Host "  Swagger:  $ApiUrl/swagger"
Write-Host "  Health:   $ApiUrl/health"
Write-Host "  Frontend: $FrontendUrl"
Write-Host "`nStreaming job output. Press Ctrl+C to stop both jobs." -ForegroundColor Yellow

try {
    while ($apiJob.State -eq 'Running' -or $feJob.State -eq 'Running') {
        Receive-Job -Job $apiJob, $feJob
        Start-Sleep -Milliseconds 500
    }
    Receive-Job -Job $apiJob, $feJob
}
finally {
    Write-Host "`nStopping jobs..." -ForegroundColor Yellow
    Stop-Job -Job $apiJob, $feJob -ErrorAction SilentlyContinue
    Remove-Job -Job $apiJob, $feJob -Force -ErrorAction SilentlyContinue
}
