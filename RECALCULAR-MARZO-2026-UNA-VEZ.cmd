@echo off
setlocal

cd /d "%~dp0"

if exist "SINCRONIZAR-CONFIG.cmd" (
    call "SINCRONIZAR-CONFIG.cmd" >nul
)

set "PATH=%USERPROFILE%\.dotnet;%USERPROFILE%\.dotnet-x86;%PATH%"
set "DOTNET_ROOT_X86=%USERPROFILE%\.dotnet-x86"

echo [Checador] ESTE PROCESO ES EXCEPCIONAL Y SOLO PARA MARZO 2026.
choice /C SN /N /M "[Checador] Continuar con reconciliacion historica masiva? S/N: "
if errorlevel 2 (
    echo [Checador] Operacion cancelada por usuario.
    exit /b 0
)

set "CHECADOR_Sql__ConnectionString=Server=TI;Database=ZKBD;Trusted_Connection=True;TrustServerCertificate=True;"
set "CHECADOR_PervasiveSync__Enabled=true"
set "CHECADOR_PervasiveSync__ConnectionString=Driver={Pervasive ODBC Client Interface};ServerName=OCISERVER;DBQ=GLOBALPIN;UID=GLOBALOCI;PWD=GLOBALPIN;"
set "CHECADOR_PervasiveSync__SimulationMode=false"
set "CHECADOR_PervasiveSync__OnlyCurrentDayWrites=false"
set "CHECADOR_PervasiveSync__CloseOnlyWithFinalOutMode=false"
set "CHECADOR_PervasiveSync__CommandTimeoutSeconds=180"

echo [Checador] Ejecutando recalculo masivo marzo 2026 (una sola vez)...
dotnet run --project "src\Checador.ConsoleApp\Checador.ConsoleApp.csproj" -- --reconcile-march-2026-once
if errorlevel 1 (
    echo.
    echo [Checador] ERROR: la reconciliacion masiva no termino correctamente.
    pause
    exit /b 1
)

echo.
echo [Checador] Recalculo masivo marzo 2026 finalizado.
pause
exit /b 0
