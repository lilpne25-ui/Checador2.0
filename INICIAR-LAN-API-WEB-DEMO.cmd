@echo off
setlocal

cd /d "%~dp0"
set "PATH=%USERPROFILE%\.dotnet;%USERPROFILE%\.dotnet-x86;%PATH%"

set "API_PROJ=src\Checador.Api\Checador.Api.csproj"
set "WEB_PROJ=src\Checador.Web\Checador.Web.csproj"

set "DEMO_SQL_SERVER=."
set "DEMO_DB=ZKBD_DEMO_WEB"
set "DEMO_EMPLOYEES=20"
set "DEMO_DAYS=365"
set "DEMO_SQL_CONN=Server=%DEMO_SQL_SERVER%;Database=%DEMO_DB%;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;"

set "API_URL=http://0.0.0.0:5185"
set "WEB_URL=http://0.0.0.0:5195"
set "API_BASE_URL=http://127.0.0.1:5185"

call :stop_running_instances

echo [DEMO] Preparando base de datos demo aislada...
powershell -NoProfile -ExecutionPolicy Bypass -File "tools\demo\Setup-WebDemoDb.ps1" -SqlServer "%DEMO_SQL_SERVER%" -Database "%DEMO_DB%" -EmployeeCount %DEMO_EMPLOYEES% -LookbackDays %DEMO_DAYS%
if errorlevel 1 (
  echo [DEMO] ERROR: no se pudo preparar la base demo.
  pause
  exit /b 1
)

echo [DEMO] Compilando API/Web (Release)...
call :build_release "%API_PROJ%"
if errorlevel 1 (
  echo [DEMO] ERROR compilando API.
  pause
  exit /b 1
)

call :build_release "%WEB_PROJ%"
if errorlevel 1 (
  echo [DEMO] ERROR compilando Web.
  pause
  exit /b 1
)

if not exist "%API_PROJ%" (
  echo [DEMO] No existe %API_PROJ%
  pause
  exit /b 1
)

if not exist "%WEB_PROJ%" (
  echo [DEMO] No existe %WEB_PROJ%
  pause
  exit /b 1
)

echo [DEMO] Iniciando Checador.Api demo en %API_URL%
start "Checador API DEMO" cmd /k "set DOTNET_ROLL_FORWARD=Major&& set CHECADOR_Sql__ConnectionString=%DEMO_SQL_CONN%&& set CHECADOR_Device__Enabled=false&& set CHECADOR_PervasiveSync__Enabled=false&& dotnet run --no-launch-profile -c Release --no-build --project %API_PROJ% -- --urls %API_URL%"

echo [DEMO] Esperando API en 127.0.0.1:5185 ...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$deadline=(Get-Date).AddSeconds(45); while((Get-Date) -lt $deadline){ if(Test-NetConnection 127.0.0.1 -Port 5185 -InformationLevel Quiet){ exit 0 }; Start-Sleep -Seconds 1 }; exit 1"
if errorlevel 1 (
  echo [DEMO] ERROR: Checador.Api demo no esta escuchando en 127.0.0.1:5185.
  echo [DEMO] Revisa la ventana "Checador API DEMO".
  pause
  exit /b 1
)

echo [DEMO] Iniciando Checador.Web demo en %WEB_URL%
start "Checador Web DEMO" cmd /k "set DOTNET_ROLL_FORWARD=Major&& set CHECADOR_DEMO_MODE=true&& set CHECADOR_WebApi__BaseUrl=%API_BASE_URL%&& dotnet run --no-launch-profile -c Release --no-build --project %WEB_PROJ% -- --urls %WEB_URL%"

echo.
echo [DEMO] Servicios lanzados en modo demo aislado.
echo [DEMO] Base demo: %DEMO_DB% (servidor %DEMO_SQL_SERVER%)
echo [DEMO] Web: http://localhost:5195/Login
echo [DEMO] API health: http://localhost:5185/health/status
echo.
echo [DEMO] Usuarios demo:
echo [DEMO]   SUPERVSR / SUPERVSR  ^(ve modulos restringidos^)
echo [DEMO]   admin1 / admin1      ^(admin normal^)
echo [DEMO]   consulta1 / consulta1 ^(consulta^)
echo.
echo [DEMO] Para detener, cierra las ventanas "Checador API DEMO" y "Checador Web DEMO".
exit /b 0

:stop_running_instances
echo [DEMO] Cerrando instancias previas de API/Web (si existen)...
taskkill /F /IM "Checador.Api.exe" >nul 2>&1
taskkill /F /IM "Checador.Web.exe" >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -Command "$procs=Get-CimInstance Win32_Process -Filter \"Name='dotnet.exe'\"; foreach($p in $procs){ $cmd=$p.CommandLine; if($cmd -and ($cmd -like '*Checador.Api*' -or $cmd -like '*Checador.Web*')){ try { Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue } catch {} } }" >nul 2>&1
timeout /t 1 /nobreak >nul
exit /b 0

:build_release
set "TARGET_PROJ=%~1"
dotnet build "%TARGET_PROJ%" -c Release --no-restore
if not errorlevel 1 exit /b 0
echo [DEMO] Build sin restore fallo; ejecutando restore y reintentando...
dotnet restore "%TARGET_PROJ%"
if errorlevel 1 exit /b 1
dotnet build "%TARGET_PROJ%" -c Release --no-restore
if not errorlevel 1 exit /b 0
echo [DEMO] Reintentando build tras cerrar procesos nuevamente...
call :stop_running_instances
dotnet build "%TARGET_PROJ%" -c Release --no-restore
exit /b %errorlevel%
