@echo off
setlocal EnableExtensions

cd /d "%~dp0"

call :sync_project "src\Checador.Desktop"
call :sync_project "src\Checador.ConsoleApp"
call :sync_project "src\Checador.SyncAgent"

echo [Config] Sincronizacion terminada.
exit /b 0

:sync_project
set "PROJECT_DIR=%~1"
set "SOURCE_CFG=%PROJECT_DIR%\appsettings.json"
set "SOURCE_LOCAL=%PROJECT_DIR%\appsettings.local.json"

if not exist "%SOURCE_CFG%" (
  echo [Config] Omitido %PROJECT_DIR% ^(sin appsettings.json fuente^).
  exit /b 0
)

call :sync_target "%SOURCE_CFG%" "%PROJECT_DIR%\bin\Debug\net8.0-windows\appsettings.json"
call :sync_target "%SOURCE_CFG%" "%PROJECT_DIR%\bin\Release\net8.0-windows\appsettings.json"
call :sync_target "%SOURCE_CFG%" "%PROJECT_DIR%\bin\Debug\net8.0-windows\win-x86\appsettings.json"
call :sync_target "%SOURCE_CFG%" "%PROJECT_DIR%\bin\Release\net8.0-windows\win-x86\appsettings.json"

if exist "%SOURCE_LOCAL%" (
  call :sync_target "%SOURCE_LOCAL%" "%PROJECT_DIR%\bin\Debug\net8.0-windows\appsettings.local.json"
  call :sync_target "%SOURCE_LOCAL%" "%PROJECT_DIR%\bin\Release\net8.0-windows\appsettings.local.json"
  call :sync_target "%SOURCE_LOCAL%" "%PROJECT_DIR%\bin\Debug\net8.0-windows\win-x86\appsettings.local.json"
  call :sync_target "%SOURCE_LOCAL%" "%PROJECT_DIR%\bin\Release\net8.0-windows\win-x86\appsettings.local.json"
)

exit /b 0

:sync_target
set "SRC=%~1"
set "DST=%~2"

for %%I in ("%DST%") do set "DST_DIR=%%~dpI"
if not exist "%DST_DIR%" (
  exit /b 0
)

copy /Y "%SRC%" "%DST%" >nul
echo [Config] Actualizado: %DST%
exit /b 0
