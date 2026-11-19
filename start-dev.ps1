# Start Development Environment - Backend + Frontend

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Starting Employee Management System  " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Kill any existing processes on ports 5167 and 5173
Write-Host "`nCleaning up existing processes..." -ForegroundColor Yellow
Get-Process | Where-Object { $_.Name -eq "dotnet" } | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process | Where-Object { $_.Name -eq "node" } | Stop-Process -Force -ErrorAction SilentlyContinue

Start-Sleep -Seconds 2

# Get the base directory
$baseDir = $PSScriptRoot

# Start Backend API
Write-Host "`n1. Starting Backend API..." -ForegroundColor Green
$backendJob = Start-Job -ScriptBlock {
    param($dir)
    Set-Location "$dir\EmployeeManagement"
    dotnet run --project "src/EmployeeManagement.Api/EmployeeManagement.Api.csproj"
} -ArgumentList $baseDir

# Wait for backend to start
Write-Host "   Waiting for backend to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Start Frontend
Write-Host "`n2. Starting Frontend React App..." -ForegroundColor Green
$frontendJob = Start-Job -ScriptBlock {
    param($dir)
    Set-Location "$dir\EmployeeManagement-FE"
    npm run dev
} -ArgumentList $baseDir

# Wait a moment for frontend to start
Start-Sleep -Seconds 3

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  ✅ Development Environment Started!  " -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

Write-Host "`n📍 Access Points:" -ForegroundColor Cyan
Write-Host "   Backend API:   http://localhost:5167" -ForegroundColor White
Write-Host "   OpenAPI Spec:  http://localhost:5167/openapi/v1.json" -ForegroundColor White
Write-Host "   Frontend:      http://localhost:5173" -ForegroundColor White

Write-Host "`n💡 Press Ctrl+C to stop the development environment" -ForegroundColor Yellow
Write-Host ""

# Keep both jobs running
$backendJob, $frontendJob | Wait-Job

Write-Host "`nShutting down..." -ForegroundColor Yellow
Stop-Job -Job $backendJob, $frontendJob
Remove-Job -Job $backendJob, $frontendJob
