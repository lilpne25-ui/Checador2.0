@echo off
setlocal

cd /d "%~dp0"
set "PATH=%USERPROFILE%\.dotnet;%USERPROFILE%\.dotnet-x86;%PATH%"

set "PROJ=src\Checador.SyncAgent\Checador.SyncAgent.csproj"
set "EXE=src\Checador.SyncAgent\bin\Release\net8.0-windows\Checador.SyncAgent.exe"
set "CFG_SRC=src\Checador.SyncAgent\appsettings.json"
set "CFG_DST=src\Checador.SyncAgent\bin\Release\net8.0-windows\appsettings.json"

echo [SyncAgent] Preparando complemento externo...
dotnet build "%PROJ%" -c Release
if errorlevel 1 (
  echo [SyncAgent] ERROR compilando SyncAgent.
  echo [SyncAgent] Cierra cualquier instancia abierta de SyncAgent y vuelve a intentar.
  exit /b 1
)

if exist "SINCRONIZAR-CONFIG.cmd" (
  call "SINCRONIZAR-CONFIG.cmd" >nul
) else (
  if exist "%CFG_SRC%" copy /Y "%CFG_SRC%" "%CFG_DST%" >nul
)

start "Checador SyncAgent" "%EXE%"
exit /b 0
