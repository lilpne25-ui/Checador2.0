@echo off
setlocal

set "SERVER=192.168.1.10"
set "PORT=1433"

echo ==================================================
echo Validando conectividad MSSQL remota
echo Servidor: %SERVER%
echo Puerto : %PORT%
echo ==================================================
echo.

where powershell >nul 2>&1
if errorlevel 1 (
  echo [ERROR] PowerShell no esta disponible en este equipo.
  endlocal & exit /b 2
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "$r=Test-NetConnection -ComputerName '%SERVER%' -Port %PORT% -WarningAction SilentlyContinue; if($r.TcpTestSucceeded){Write-Host ([string]::Format('[OK] Conexion TCP exitosa a {0}:{1}', $r.RemoteAddress, $r.RemotePort)) -ForegroundColor Green; exit 0}else{Write-Host '[FALLO] No hay conexion TCP a %SERVER%:%PORT%.' -ForegroundColor Red; Write-Host ([string]::Format('Detalle: Ping={0}; NameResolved={1}', $r.PingSucceeded, $r.NameResolutionSucceeded)); exit 1}"

set "RC=%ERRORLEVEL%"
echo.
if "%RC%"=="0" (
  echo Prueba completada correctamente.
) else (
  echo Prueba completada con error.
)

endlocal & exit /b %RC%
