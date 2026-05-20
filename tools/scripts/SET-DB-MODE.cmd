@echo off
setlocal

if "%~1"=="" (
    echo [DB-MODE] ERROR: falta nombre de base de datos.
    echo [DB-MODE] Uso: SET-DB-MODE.cmd NOMBRE_DB ETIQUETA
    exit /b 1
)

set "TARGET_DB=%~1"
set "MODE_LABEL=%~2"
if "%MODE_LABEL%"=="" set "MODE_LABEL=PERSONALIZADO"

cd /d "%~dp0\..\.."

echo [DB-MODE] Modo: %MODE_LABEL%
echo [DB-MODE] Base destino: %TARGET_DB%

call :update_project "src\Checador.Desktop"
if errorlevel 1 exit /b 1
call :update_project "src\Checador.ConsoleApp"
if errorlevel 1 exit /b 1
call :update_project "src\Checador.SyncAgent"
if errorlevel 1 exit /b 1
call :update_project "src\Checador.Api"
if errorlevel 1 exit /b 1

echo [DB-MODE] OK: configuracion local actualizada para %MODE_LABEL%.
exit /b 0

:update_project
set "PROJ_DIR=%~1"
set "LOCAL_JSON=%PROJ_DIR%\appsettings.local.json"
set "EXAMPLE_JSON=%PROJ_DIR%\appsettings.local.example.json"

if not exist "%LOCAL_JSON%" (
    if exist "%EXAMPLE_JSON%" (
        copy /Y "%EXAMPLE_JSON%" "%LOCAL_JSON%" >nul
        echo [DB-MODE] Creado %LOCAL_JSON% desde example.
    ) else (
        echo [DB-MODE] ERROR: no existe %LOCAL_JSON% ni %EXAMPLE_JSON%.
        exit /b 1
    )
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$path='%LOCAL_JSON%';" ^
  "$db='%TARGET_DB%';" ^
  "$raw=Get-Content -Raw -LiteralPath $path;" ^
  "$json=$raw | ConvertFrom-Json;" ^
  "if(-not $json.PSObject.Properties['Sql']){$json|Add-Member -NotePropertyName Sql -NotePropertyValue ([pscustomobject]@{})};" ^
  "$current=[string]$json.Sql.ConnectionString;" ^
  "if([string]::IsNullOrWhiteSpace($current)){$current='Server=tcp:SQL_HOST,1433;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;'}" ^
  "$withoutDb=[regex]::Replace($current,'(?i);?Database=[^;]*','');" ^
  "$withoutDb=$withoutDb.Trim().TrimEnd(';');" ^
  "if([string]::IsNullOrWhiteSpace($withoutDb)){$current='Database='+$db+';'} else {$current=$withoutDb+';Database='+$db+';'}" ^
  "$json.Sql.ConnectionString=$current;" ^
  "$output=$json | ConvertTo-Json -Depth 30;" ^
  "[System.IO.File]::WriteAllText($path,$output,[System.Text.UTF8Encoding]::new($false));"
if errorlevel 1 (
    echo [DB-MODE] ERROR actualizando %LOCAL_JSON%.
    exit /b 1
)

echo [DB-MODE] Actualizado: %LOCAL_JSON%
exit /b 0
