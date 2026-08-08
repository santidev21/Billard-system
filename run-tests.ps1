$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$backend = Join-Path $root "backend"
$frontend = Join-Path $root "frontend"

Write-Host "=== PRUEBAS BACKEND (.NET) ===" -ForegroundColor Cyan
& dotnet test (Join-Path $backend "BilliardSystem.slnx") --nologo
$backendExit = $LASTEXITCODE
Write-Host "Backend exit code: $backendExit" -ForegroundColor $(if ($backendExit -eq 0) { "Green" } else { "Red" })

Write-Host ""
Write-Host "=== PRUEBAS FRONTEND (Angular/Karma) ===" -ForegroundColor Cyan
Push-Location $frontend
npm test -- --watch=false --browsers=ChromeHeadless
$frontendExit = $LASTEXITCODE
Pop-Location
Write-Host "Frontend exit code: $frontendExit" -ForegroundColor $(if ($frontendExit -eq 0) { "Green" } else { "Red" })

Write-Host ""
if (($backendExit -eq 0) -and ($frontendExit -eq 0)) {
  Write-Host "TODO OK: backend + frontend pasan las pruebas." -ForegroundColor Green
} else {
  Write-Host "Algunas pruebas fallaron (backend=$backendExit, frontend=$frontendExit)." -ForegroundColor Red
}