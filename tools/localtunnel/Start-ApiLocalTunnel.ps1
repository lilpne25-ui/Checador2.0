param(
    [int]$ApiPort = 5085,
    [string]$VercelOrigin = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $repoRoot

$apiProject = "src\Checador.Api\Checador.Api.csproj"
$apiUrl = "http://127.0.0.1:$ApiPort"
$dotnetPath = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path $dotnetPath)) {
    $dotnetPath = "dotnet"
}

if (-not (Test-Path $apiProject)) {
    throw "No existe el proyecto API: $apiProject"
}

function Stop-ApiProcesses {
    cmd /c "taskkill /F /IM Checador.Api.exe >nul 2>&1" | Out-Null
    $dotnetApi = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object {
        $_.CommandLine -and $_.CommandLine -like "*Checador.Api*"
    }
    foreach ($proc in $dotnetApi) {
        try { Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue } catch {}
    }
}

function Stop-LocalTunnelProcesses {
    $ltNode = Get-CimInstance Win32_Process -Filter "Name='node.exe'" | Where-Object {
        $_.CommandLine -and $_.CommandLine -like "*localtunnel*"
    }
    foreach ($proc in $ltNode) {
        try { Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue } catch {}
    }
}

Write-Host "[LT] Cerrando procesos previos API/localtunnel..."
Stop-ApiProcesses
Stop-LocalTunnelProcesses
Start-Sleep -Seconds 1

Write-Host "[LT] Compilando API Release..."
& $dotnetPath build $apiProject -c Release --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "[LT] Build sin restore fallo, ejecutando restore..."
    & $dotnetPath restore $apiProject
    if ($LASTEXITCODE -ne 0) {
        throw "Fallo dotnet restore."
    }
    & $dotnetPath build $apiProject -c Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "Fallo dotnet build."
    }
}

$env:CHECADOR_Cors__AllowedOrigins__0 = "https://*.loca.lt"
if (-not [string]::IsNullOrWhiteSpace($VercelOrigin)) {
    $env:CHECADOR_Cors__AllowedOrigins__1 = $VercelOrigin.Trim()
}
$env:DOTNET_ROLL_FORWARD = "Major"

$apiOut = Join-Path $repoRoot "tmp_api_lt_out.log"
$apiErr = Join-Path $repoRoot "tmp_api_lt_err.log"
$ltOut = Join-Path $repoRoot "tmp_localtunnel_out.log"
$ltErr = Join-Path $repoRoot "tmp_localtunnel_err.log"
$ltUrlFile = Join-Path $repoRoot "tmp_localtunnel_url.txt"

Remove-Item $apiOut,$apiErr,$ltOut,$ltErr,$ltUrlFile -ErrorAction SilentlyContinue

Write-Host "[LT] Iniciando API en $apiUrl ..."
$apiProc = Start-Process -FilePath $dotnetPath `
    -ArgumentList @("run","--no-launch-profile","-c","Release","--no-build","--project",$apiProject,"--","--urls",$apiUrl) `
    -WorkingDirectory $repoRoot `
    -PassThru `
    -WindowStyle Hidden `
    -RedirectStandardOutput $apiOut `
    -RedirectStandardError $apiErr

$deadline = (Get-Date).AddSeconds(60)
$ready = $false
while ((Get-Date) -lt $deadline) {
    if (Test-NetConnection 127.0.0.1 -Port $ApiPort -InformationLevel Quiet) {
        $ready = $true
        break
    }
    Start-Sleep -Milliseconds 700
}

if (-not $ready) {
    throw "La API no quedo escuchando en 127.0.0.1:$ApiPort. Revisa $apiErr"
}

Write-Host "[LT] Iniciando localtunnel..."
$ltProc = Start-Process -FilePath "cmd.exe" `
    -ArgumentList @("/c","npx --yes localtunnel --port $ApiPort") `
    -WorkingDirectory $repoRoot `
    -PassThru `
    -WindowStyle Hidden `
    -RedirectStandardOutput $ltOut `
    -RedirectStandardError $ltErr

$url = $null
$deadline = (Get-Date).AddSeconds(90)
$regex = [regex]"https://[a-z0-9\-]+\.loca\.lt"
while ((Get-Date) -lt $deadline) {
    if (Test-Path $ltOut) {
        try {
            $content = Get-Content $ltOut -Raw -ErrorAction SilentlyContinue
            if (-not [string]::IsNullOrWhiteSpace($content)) {
                $match = $regex.Match($content)
                if ($match.Success) {
                    $url = $match.Value
                    break
                }
            }
        } catch {
            # El proceso puede bloquear el log temporalmente; reintentar.
        }
    }
    Start-Sleep -Milliseconds 800
}

if ([string]::IsNullOrWhiteSpace($url)) {
    if (Test-Path $ltOut) {
        $fallback = Get-Content $ltOut -Raw -ErrorAction SilentlyContinue
        if (-not [string]::IsNullOrWhiteSpace($fallback)) {
            $fallbackMatch = $regex.Match($fallback)
            if ($fallbackMatch.Success) {
                $url = $fallbackMatch.Value
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($url)) {
    throw "No se obtuvo URL de localtunnel. Revisa $ltOut y $ltErr"
}

$url | Set-Content -Path $ltUrlFile -Encoding UTF8

Write-Host ""
Write-Host "[LT] URL publica: $url"
Write-Host "[LT] Health: $url/health/status"
Write-Host "[LT] URL guardada en: $ltUrlFile"
Write-Host "[LT] CORS activo para: https://*.loca.lt" -ForegroundColor Yellow
if (-not [string]::IsNullOrWhiteSpace($VercelOrigin)) {
    Write-Host "[LT] CORS adicional: $VercelOrigin" -ForegroundColor Yellow
}
Write-Host ""
Write-Host "[LT] API PID: $($apiProc.Id)"
Write-Host "[LT] Tunnel PID: $($ltProc.Id)"
Write-Host "[LT] No cierres estos procesos si quieres mantener la URL activa."
