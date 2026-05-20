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
    set "APPSETTINGS_LOCAL_SRC=appsettings.local.json"
    set "APPSETTINGS_LOCAL_DST=appsettings.local.json"
) else (
    set "PROJ=src\Checador.Desktop\Checador.Desktop.csproj"
    set "EXE=src\Checador.Desktop\bin\Release\net8.0-windows\Checador.Desktop.exe"
    set "APPSETTINGS_SRC=src\Checador.Desktop\appsettings.json"
    set "APPSETTINGS_DST=src\Checador.Desktop\bin\Release\net8.0-windows\appsettings.json"
    set "APPSETTINGS_LOCAL_SRC=src\Checador.Desktop\appsettings.local.json"
    set "APPSETTINGS_LOCAL_DST=src\Checador.Desktop\bin\Release\net8.0-windows\appsettings.local.json"
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

if exist "%APPSETTINGS_LOCAL_SRC%" (
    copy /Y "%APPSETTINGS_LOCAL_SRC%" "%APPSETTINGS_LOCAL_DST%" >nul
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

call :launch_foreground "%EXE%"
exit /b %errorlevel%

:launch_foreground
set "LAUNCH_EXE=%~1"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$exe=(Resolve-Path -LiteralPath $env:LAUNCH_EXE -ErrorAction Stop).Path; $p=Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Normal; try { $ws=New-Object -ComObject WScript.Shell; for($i=0; $i -lt 8; $i++){ if($ws.AppActivate($p.Id)){ break }; Start-Sleep -Milliseconds 200 } } catch {}" >nul 2>&1
if errorlevel 1 (
    start "Checador 2.0" /D "%~dp1" "%~f1"
    exit /b 0
)
exit /b 0

:build_release
dotnet build "%PROJ%" -c Release --no-restore
if not errorlevel 1 exit /b 0
echo [Checador] Build sin restore fallo; ejecutando restore y reintentando...
dotnet restore "%PROJ%"
if errorlevel 1 exit /b 1
dotnet build "%PROJ%" -c Release --no-restore
exit /b %errorlevel%