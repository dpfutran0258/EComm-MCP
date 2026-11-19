#!/usr/bin/env pwsh

# =====================================================
# Employee Management System - Gemini AI Edition
# Development Environment Startup Script
# =====================================================

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  🤖 Employee Management - Gemini AI Edition 🤖  ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Configuration
$BACKEND_DIR = "EmployeeManagement"
$FRONTEND_DIR = "EmployeeManagement-FE"
$BACKEND_PORT = 5065
$FRONTEND_PORT = 5173

# Pre-flight checks
Write-Host "🔍 Running pre-flight checks..." -ForegroundColor Yellow
Write-Host ""

# Check .NET
Write-Host "Checking .NET installation..." -ForegroundColor Gray
try {
    $dotnetVersion = dotnet --version
    Write-Host "✅ .NET SDK version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ .NET SDK not found. Please install .NET 10.0 SDK" -ForegroundColor Red
    exit 1
}

# Check Node.js
Write-Host "Checking Node.js installation..." -ForegroundColor Gray
try {
    $nodeVersion = node --version
    Write-Host "✅ Node.js version: $nodeVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ Node.js not found. Please install Node.js 18+" -ForegroundColor Red
    exit 1
}

# Check PostgreSQL (optional - will warn but not fail)
Write-Host "Checking PostgreSQL..." -ForegroundColor Gray
try {
    $pgTest = Test-NetConnection -ComputerName localhost -Port 5432 -WarningAction SilentlyContinue -ErrorAction SilentlyContinue
    if ($pgTest.TcpTestSucceeded) {
        Write-Host "✅ PostgreSQL is running on port 5432" -ForegroundColor Green
    } else {
        Write-Host "⚠️  PostgreSQL not detected on port 5432" -ForegroundColor Yellow
        Write-Host "   Please start PostgreSQL before using the application" -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠️  Could not verify PostgreSQL status" -ForegroundColor Yellow
}

# Check Gemini API Key
Write-Host "Checking Gemini API Key configuration..." -ForegroundColor Gray
$appsettingsPath = Join-Path $BACKEND_DIR "src\EmployeeManagement.Api\appsettings.json"
if (Test-Path $appsettingsPath) {
    $appsettings = Get-Content $appsettingsPath | ConvertFrom-Json
    if ($appsettings.Gemini.ApiKey -and $appsettings.Gemini.ApiKey -ne "YOUR_GEMINI_API_KEY_HERE") {
        Write-Host "✅ Gemini API Key is configured" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Gemini API Key not configured" -ForegroundColor Yellow
        Write-Host "   Edit $appsettingsPath and add your API key" -ForegroundColor Yellow
        Write-Host "   Get one at: https://makersuite.google.com/app/apikey" -ForegroundColor Cyan
    }
} else {
    Write-Host "⚠️  appsettings.json not found" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Start Backend
Write-Host "🚀 Starting Backend API..." -ForegroundColor Yellow
Write-Host ""

if (Test-Path $BACKEND_DIR) {
    Set-Location $BACKEND_DIR
    
    Write-Host "   📦 Restoring packages..." -ForegroundColor Gray
    dotnet restore --verbosity quiet
    
    Write-Host "   🔨 Building backend..." -ForegroundColor Gray
    $buildResult = dotnet build --no-restore --verbosity quiet 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ Backend built successfully" -ForegroundColor Green
        Write-Host ""
        Write-Host "   🌐 Starting API on http://localhost:$BACKEND_PORT" -ForegroundColor Cyan
        Write-Host ""
        
        # Start backend in background
        Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$PWD'; dotnet run --project src\EmployeeManagement.Api --no-build" -WindowStyle Normal
        
        Write-Host "   ✅ Backend started in new window" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Backend build failed:" -ForegroundColor Red
        Write-Host $buildResult -ForegroundColor Red
        Set-Location ..
        exit 1
    }
    
    Set-Location ..
} else {
    Write-Host "   ❌ Backend directory not found: $BACKEND_DIR" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "⏳ Waiting for backend to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Start Frontend
Write-Host ""
Write-Host "🚀 Starting Frontend..." -ForegroundColor Yellow
Write-Host ""

if (Test-Path $FRONTEND_DIR) {
    Set-Location $FRONTEND_DIR
    
    # Check if node_modules exists
    if (-not (Test-Path "node_modules")) {
        Write-Host "   📦 Installing frontend dependencies..." -ForegroundColor Gray
        npm install --silent
    }
    
    Write-Host "   🌐 Starting frontend on http://localhost:$FRONTEND_PORT" -ForegroundColor Cyan
    Write-Host ""
    
    # Start frontend in background
    Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$PWD'; npm run dev" -WindowStyle Normal
    
    Write-Host "   ✅ Frontend started in new window" -ForegroundColor Green
    
    Set-Location ..
} else {
    Write-Host "   ❌ Frontend directory not found: $FRONTEND_DIR" -ForegroundColor Red
    Set-Location ..
    exit 1
}

# Success message
Write-Host ""
Write-Host "════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "✅ Development environment started successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "🌐 Access the application at:" -ForegroundColor Cyan
Write-Host "   Frontend:  http://localhost:$FRONTEND_PORT" -ForegroundColor White
Write-Host "   Backend:   http://localhost:$BACKEND_PORT" -ForegroundColor White
Write-Host "   Swagger:   http://localhost:$BACKEND_PORT/swagger" -ForegroundColor White
Write-Host ""
Write-Host "💬 Open the chatbot by clicking the chat icon (💬)" -ForegroundColor Yellow
Write-Host "   in the bottom-right corner of the page" -ForegroundColor Yellow
Write-Host ""
Write-Host "🤖 Powered by Google Gemini AI" -ForegroundColor Magenta
Write-Host ""
Write-Host "📖 For help, see README_GEMINI_AI.md" -ForegroundColor Gray
Write-Host ""
Write-Host "Press any key to open the browser..." -ForegroundColor Cyan
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# Open browser
Start-Process "http://localhost:$FRONTEND_PORT"

Write-Host ""
Write-Host "✨ Enjoy your AI-powered Employee Management System!" -ForegroundColor Green
Write-Host ""
