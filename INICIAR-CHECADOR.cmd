@echo off
setlocal

cd /d "%~dp0"

if exist "SINCRONIZAR-CONFIG.cmd" (
    call "SINCRONIZAR-CONFIG.cmd" >nul
)

set "PATH=%USERPROFILE%\.dotnet;%USERPROFILE%\.dotnet-x86;%PATH%"
set "DOTNET_ROOT_X86=%USERPROFILE%\.dotnet-x86"

set "MODE=DEV"
if exist "Checador.Desktop.exe" (
    set "MODE=PORTABLE"
)

if /I "%MODE%"=="PORTABLE" (
    set "EXE=Checador.Desktop.exe"
    set "APPSETTINGS_SRC=appsettings.json"
    set "APPSETTINGS_DST=appsettings.json"
) else (
    set "PROJ=src\Checador.Desktop\Checador.Desktop.csproj"
    set "EXE=src\Checador.Desktop\bin\Release\net8.0-windows\Checador.Desktop.exe"
    set "APPSETTINGS_SRC=src\Checador.Desktop\appsettings.json"
    set "APPSETTINGS_DST=src\Checador.Desktop\bin\Release\net8.0-windows\appsettings.json"
)

if /I "%MODE%"=="DEV" (
    echo [Checador] Compilando Release para asegurar version/configuracion actual...
    tasklist /FI "IMAGENAME eq Checador.Desktop.exe" | find /I "Checador.Desktop.exe" >nul
    if errorlevel 1 (
        call :build_release
        if errorlevel 1 (
            echo.
            echo [Checador] Aviso: no se pudo compilar.
            echo [Checador] Si esperabas ver cambios nuevos, esta ejecucion no los incluira.
            if not exist "%EXE%" (
                echo [Checador] No existe ejecutable previo para continuar.
                pause
                exit /b 1
            )
        )
    ) else (
        echo [Checador] App abierta detectada.
        choice /C SC /N /M "[Checador] S= cerrar app y recompilar, C= continuar sin recompilar: "
        if errorlevel 2 (
            echo.
            echo [Checador] AVISO: abriras la version ya compilada ^(sin cambios nuevos^).
        ) else (
            echo.
            echo [Checador] Cerrando app para compilar...
            taskkill /F /IM "Checador.Desktop.exe" >nul 2>&1
            timeout /t 1 /nobreak >nul
            call :build_release
            if errorlevel 1 (
                echo [Checador] ERROR: fallo la compilacion despues de cerrar la app.
                echo [Checador] Se abrira la ultima version compilada disponible.
                if not exist "%EXE%" (
                    echo [Checador] No existe ejecutable previo para continuar.
                    pause
                    exit /b 1
                )
            )
        )
    )
) else (
    echo [Checador] Modo portable detectado: se ejecutara sin compilar.
)

if exist "%APPSETTINGS_SRC%" (
    copy /Y "%APPSETTINGS_SRC%" "%APPSETTINGS_DST%" >nul
)

if not exist "%EXE%" (
    echo.
    echo [Checador] No se pudo localizar: %EXE%
    pause
    exit /b 1
)

for %%I in ("%EXE%") do set "EXE_TS=%%~tI"
if defined EXE_TS (
    echo [Checador] Ejecutable Release detectado. Ultima compilacion: %EXE_TS%
)

start "Checador 2.0" "%EXE%"
exit /b 0

:build_release
dotnet build "%PROJ%" -c Release --no-restore
if not errorlevel 1 exit /b 0
echo [Checador] Build sin restore fallo; ejecutando restore y reintentando...
dotnet restore "%PROJ%"
if errorlevel 1 exit /b 1
dotnet build "%PROJ%" -c Release --no-restore
exit /b %errorlevel%