@echo off
setlocal

set "API_HEALTH_URL=https://localhost:5051/health"
set "API_HEALTH_TIMEOUT=60"

cd /d "%~dp0"

echo Iniciando PharmazapPlus.API...
start "PharmazapPlus API" cmd /k "cd /d \"%~dp0src\PharmazapPlus.API\" && dotnet run"

echo Aguardando API ficar pronta...
powershell -NoProfile -Command ^
  "[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }; " ^
  "$maxAttempts = %API_HEALTH_TIMEOUT%; $ready = $false; " ^
  "for ($i = 0; $i -lt $maxAttempts; $i++) { " ^
  "  try { Invoke-WebRequest -Uri '%API_HEALTH_URL%' -UseBasicParsing -TimeoutSec 2 | Out-Null; $ready = $true; break } " ^
  "  catch { Start-Sleep -Seconds 1 } " ^
  "} " ^
  "if (-not $ready) { exit 1 }"

if errorlevel 1 (
  echo API nao respondeu em ate %API_HEALTH_TIMEOUT%s; iniciando WEB mesmo assim.
)

echo Iniciando PharmazapPlus.Web...
start "PharmazapPlus WEB" cmd /k "cd /d \"%~dp0src\PharmazapPlus.Web\" && dotnet run"

echo API e WEB iniciados.
endlocal
