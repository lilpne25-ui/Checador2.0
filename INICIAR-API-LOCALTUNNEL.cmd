@echo off
setlocal

cd /d "%~dp0"
set "PATH=%USERPROFILE%\.dotnet;%USERPROFILE%\.dotnet-x86;%PATH%"

set "VERCEL_ORIGIN=%~1"

if "%VERCEL_ORIGIN%"=="" (
  powershell -NoProfile -ExecutionPolicy Bypass -File "tools\localtunnel\Start-ApiLocalTunnel.ps1"
) else (
  powershell -NoProfile -ExecutionPolicy Bypass -File "tools\localtunnel\Start-ApiLocalTunnel.ps1" -VercelOrigin "%VERCEL_ORIGIN%"
)

if errorlevel 1 (
  echo.
  echo [LT] ERROR al iniciar API + localtunnel.
  echo [LT] Revisa logs: tmp_api_lt_err.log, tmp_localtunnel_err.log
  pause
  exit /b 1
)

echo.
echo [LT] Listo.
echo [LT] Usa la URL de: tmp_localtunnel_url.txt
exit /b 0
