$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$backendApi = Join-Path $root "backend\src\BilliardSystem.API"
$frontend = Join-Path $root "frontend"

Write-Host "Deteniendo instancias previas (backend, node)..." -ForegroundColor Cyan
Get-Process -Name "BilliardSystem.API","dotnet","node" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

Write-Host "Compilando backend..." -ForegroundColor Cyan
dotnet build (Join-Path $backendApi "BilliardSystem.API.csproj") -c Debug --nologo 2>&1 | Select-Object -Last 1

Write-Host "Levantando backend en localhost:5000 ..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit","-Command","Set-Location '$backendApi'; dotnet run --no-build" -WorkingDirectory $backendApi

Write-Host "Levantando frontend en localhost:4200 ..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit","-Command","Set-Location '$frontend'; npm start" -WorkingDirectory $frontend

Start-Sleep -Seconds 5
try {
  $r = Invoke-WebRequest -Uri http://localhost:5000/api/tables -UseBasicParsing -TimeoutSec 3
  Write-Host "Backend OK ($($r.StatusCode))" -ForegroundColor Green
} catch {
  Write-Host "Backend aun arrancando (espera unos segundos mas)." -ForegroundColor Yellow
}

Write-Host "Backend: http://localhost:5000  |  Frontend: http://localhost:4200" -ForegroundColor Green