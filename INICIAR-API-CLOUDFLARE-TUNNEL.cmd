@echo off
setlocal

cd /d "%~dp0"
set "PATH=%USERPROFILE%\.dotnet;%USERPROFILE%\.dotnet-x86;%PATH%"

set "API_PROJ=src\Checador.Api\Checador.Api.csproj"
set "API_URL=http://127.0.0.1:5085"
set "API_PORT=5085"
set "CLOUDFLARED_EXE=cloudflared"

if not exist "%API_PROJ%" (
  echo [TUNNEL] No existe %API_PROJ%
  pause
  exit /b 1
)

if "%CHECADOR_TUNNEL_TOKEN%"=="" (
  echo [TUNNEL] Falta variable CHECADOR_TUNNEL_TOKEN.
  echo [TUNNEL] Ejemplo PowerShell:
  echo [TUNNEL]   $env:CHECADOR_TUNNEL_TOKEN="pega_aqui_el_token_de_cloudflare"
  echo [TUNNEL]   $env:CHECADOR_CORS_ORIGIN="https://tu-frontend.vercel.app"
  echo [TUNNEL]   .\INICIAR-API-CLOUDFLARE-TUNNEL.cmd
  pause
  exit /b 1
)

if defined CHECADOR_CORS_ORIGIN (
  if not defined CHECADOR_Cors__AllowedOrigins__0 (
    set "CHECADOR_Cors__AllowedOrigins__0=%CHECADOR_CORS_ORIGIN%"
  )
)

if defined CHECADOR_CORS_ORIGIN_2 (
  if not defined CHECADOR_Cors__AllowedOrigins__1 (
    set "CHECADOR_Cors__AllowedOrigins__1=%CHECADOR_CORS_ORIGIN_2%"
  )
)

if not defined CHECADOR_Cors__AllowedOrigins__0 (
  echo [TUNNEL] AVISO: no hay origen CORS configurado.
  echo [TUNNEL] AVISO: define CHECADOR_CORS_ORIGIN o CHECADOR_Cors__AllowedOrigins__0.
)

echo [TUNNEL] Cerrando instancias previas de API (si existen)...
taskkill /F /IM "Checador.Api.exe" >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -Command "$procs=Get-CimInstance Win32_Process -Filter \"Name='dotnet.exe'\"; foreach($p in $procs){ $cmd=$p.CommandLine; if($cmd -and $cmd -like '*Checador.Api*'){ try { Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue } catch {} } }" >nul 2>&1
timeout /t 1 /nobreak >nul

echo [TUNNEL] Compilando API (Release)...
dotnet build "%API_PROJ%" -c Release --no-restore
if errorlevel 1 (
  echo [TUNNEL] Build sin restore fallo; ejecutando restore y reintentando...
  dotnet restore "%API_PROJ%"
  if errorlevel 1 (
    echo [TUNNEL] ERROR en restore.
    pause
    exit /b 1
  )
  dotnet build "%API_PROJ%" -c Release --no-restore
  if errorlevel 1 (
    echo [TUNNEL] ERROR compilando API.
    pause
    exit /b 1
  )
)

echo [TUNNEL] Iniciando Checador.Api en %API_URL%
start "Checador API Tunnel" cmd /k "set DOTNET_ROLL_FORWARD=Major&& dotnet run --no-launch-profile -c Release --no-build --project %API_PROJ% -- --urls %API_URL%"

echo [TUNNEL] Esperando API en 127.0.0.1:%API_PORT% ...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$deadline=(Get-Date).AddSeconds(45); while((Get-Date) -lt $deadline){ if(Test-NetConnection 127.0.0.1 -Port %API_PORT% -InformationLevel Quiet){ exit 0 }; Start-Sleep -Seconds 1 }; exit 1"
if errorlevel 1 (
  echo [TUNNEL] ERROR: Checador.Api no esta escuchando en 127.0.0.1:%API_PORT%.
  echo [TUNNEL] Revisa la ventana "Checador API Tunnel".
  pause
  exit /b 1
)

where cloudflared >nul 2>&1
if errorlevel 1 (
  if exist "tools\cloudflared\cloudflared.exe" (
    set "CLOUDFLARED_EXE=%CD%\tools\cloudflared\cloudflared.exe"
  ) else (
    echo [TUNNEL] ERROR: cloudflared no esta instalado o no esta en PATH.
    echo [TUNNEL] Descarga manual:
    echo [TUNNEL]   https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe
    echo [TUNNEL] O coloca el exe en tools\cloudflared\cloudflared.exe
    pause
    exit /b 1
  )
)

echo [TUNNEL] Iniciando Cloudflare Tunnel (usa token en CHECADOR_TUNNEL_TOKEN)...
start "Cloudflare Tunnel API" cmd /k "\"%CLOUDFLARED_EXE%\" tunnel --no-autoupdate run --token %CHECADOR_TUNNEL_TOKEN%"

echo.
echo [TUNNEL] API local: %API_URL%
echo [TUNNEL] Health local: http://127.0.0.1:%API_PORT%/health/status
echo [TUNNEL] Frontend remoto: usa el hostname configurado en Cloudflare Zero Trust.
echo [TUNNEL] Si tu frontend esta en Vercel, apunta sus requests al dominio publico del tunnel.
echo.
echo [TUNNEL] Para detener, cierra ambas ventanas:
echo [TUNNEL]   - "Checador API Tunnel"
echo [TUNNEL]   - "Cloudflare Tunnel API"

exit /b 0
