@echo off
setlocal

cd /d "%~dp0"
set "PATH=%USERPROFILE%\.dotnet;%USERPROFILE%\.dotnet-x86;%PATH%"

set "API_PROJ=src\Checador.Api\Checador.Api.csproj"
set "WEB_PROJ=src\Checador.Web\Checador.Web.csproj"
set "API_URL=http://0.0.0.0:5085"
set "WEB_URL=http://0.0.0.0:5095"

call :stop_running_instances

echo [LAN] Compilando API/Web (Release)...
call :build_release "%API_PROJ%"
if errorlevel 1 (
  echo [LAN] ERROR compilando API.
  echo [LAN] Se intento cerrar procesos API activos automaticamente.
  pause
  exit /b 1
)

call :build_release "%WEB_PROJ%"
if errorlevel 1 (
  echo [LAN] ERROR compilando Web.
  echo [LAN] Se intento cerrar procesos Web activos automaticamente.
  pause
  exit /b 1
)

if not exist "%API_PROJ%" (
  echo [LAN] No existe %API_PROJ%
  pause
  exit /b 1
)

if not exist "%WEB_PROJ%" (
  echo [LAN] No existe %WEB_PROJ%
  pause
  exit /b 1
)

echo [LAN] Iniciando Checador.Api en %API_URL%
start "Checador API LAN" cmd /k "set DOTNET_ROLL_FORWARD=Major&& dotnet run --no-launch-profile -c Release --no-build --project %API_PROJ% -- --urls %API_URL%"

echo [LAN] Esperando API en http://127.0.0.1:5085 ...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$deadline=(Get-Date).AddSeconds(45); while((Get-Date) -lt $deadline){ if(Test-NetConnection 127.0.0.1 -Port 5085 -InformationLevel Quiet){ exit 0 }; Start-Sleep -Seconds 1 }; exit 1"
if errorlevel 1 (
  echo [LAN] ERROR: Checador.Api no esta escuchando en 127.0.0.1:5085.
  echo [LAN] Revisa la ventana "Checador API LAN" para ver el error real.
  pause
  exit /b 1
)

echo [LAN] Iniciando Checador.Web en %WEB_URL%
start "Checador Web LAN" cmd /k "set DOTNET_ROLL_FORWARD=Major&& dotnet run --no-launch-profile -c Release --no-build --project %WEB_PROJ% -- --urls %WEB_URL%"

echo.
echo [LAN] Servicios lanzados.
echo [LAN] Web: http://localhost:5095/Login
echo [LAN] API health: http://localhost:5085/health/status
echo.
echo [LAN] Cada servicio corre en su propia ventana.
echo [LAN] Para detenerlos, cierra esas ventanas o presiona Ctrl+C en cada una.

exit /b 0

:stop_running_instances
echo [LAN] Cerrando instancias previas de API/Web (si existen)...
taskkill /F /IM "Checador.Api.exe" >nul 2>&1
taskkill /F /IM "Checador.Web.exe" >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -Command "$procs=Get-CimInstance Win32_Process -Filter \"Name='dotnet.exe'\"; foreach($p in $procs){ $cmd=$p.CommandLine; if($cmd -and ($cmd -like '*Checador.Api*' -or $cmd -like '*Checador.Web*')){ try { Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue } catch {} } }" >nul 2>&1
timeout /t 1 /nobreak >nul
exit /b 0

:build_release
set "TARGET_PROJ=%~1"
dotnet build "%TARGET_PROJ%" -c Release --no-restore
if not errorlevel 1 exit /b 0
echo [LAN] Build sin restore fallo; ejecutando restore y reintentando...
dotnet restore "%TARGET_PROJ%"
if errorlevel 1 exit /b 1
dotnet build "%TARGET_PROJ%" -c Release --no-restore
if not errorlevel 1 exit /b 0
echo [LAN] Reintentando build tras cerrar procesos nuevamente...
call :stop_running_instances
dotnet build "%TARGET_PROJ%" -c Release --no-restore
exit /b %errorlevel%
