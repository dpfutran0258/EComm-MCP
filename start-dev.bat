@echo off
setlocal enabledelayedexpansion

echo.
echo ========================================
echo   Starting Employee Management System
echo ========================================
echo.

REM Kill existing processes
taskkill /F /IM dotnet.exe >nul 2>&1
taskkill /F /IM node.exe >nul 2>&1

timeout /t 2 /nobreak

REM Start Backend in new window
echo Starting Backend API...
start "Employee Management API" cmd /k "cd /d C:\Users\PiyushDhale\source\repos\EComm-MCP\EmployeeManagement\src\EmployeeManagement.Api && dotnet run"

timeout /t 5 /nobreak

REM Start Frontend in new window
echo Starting Frontend...
start "Employee Management UI" cmd /k "cd /d C:\Users\PiyushDhale\source\repos\EComm-MCP\employee-management-ui && npm run dev"

echo.
echo ========================================
echo   Development Environment Started!
echo ========================================
echo.
echo Access Points:
echo   Backend API:   http://localhost:5167
echo   Swagger UI:    http://localhost:5167/scalar/v1
echo   Frontend:      http://localhost:5173
echo.
echo Both windows will open automatically.
pause
