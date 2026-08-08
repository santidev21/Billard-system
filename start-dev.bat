@echo off
setlocal
REM ============================================
REM  BILLIARD SYSTEM - Dev Launcher
REM  Levanta la API (puerto 5000) y el frontend
REM  Angular (puerto 4200) en ventanas separadas.
REM ============================================

set ROOT=%~dp0

echo [1/2] Iniciando Backend (BilliardSystem.API en http://localhost:5000)...
start "BilliardSystem.API" cmd /k "cd /d "%ROOT%backend\src\BilliardSystem.API" && dotnet run"

echo [2/2] Iniciando Frontend (Angular en http://localhost:4200)...
start "BilliardSystem-Frontend" cmd /k "cd /d "%ROOT%frontend" && npm start"

echo.
echo Levantado. Espera unos segundos y abre http://localhost:4200
endlocal