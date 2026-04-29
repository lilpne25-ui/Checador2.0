param(
    [Parameter(Mandatory = $false)]
    [string]$DllPath,

    [Parameter(Mandatory = $false)]
    [switch]$SkipSearch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-IsAdmin {
    $current = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($current)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-RegistryDefaultValue {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        return $null
    }

    try {
        return (Get-ItemProperty -Path $Path).'(default)'
    }
    catch {
        return $null
    }
}

function Get-ProgIdStatus {
    param([string]$ProgId)

    $progPath = "Registry::HKEY_CLASSES_ROOT\$ProgId"
    $exists = Test-Path $progPath
    $clsid = $null
    $inprocPath = $null
    $dll = $null

    if ($exists) {
        $clsid = Get-RegistryDefaultValue -Path "$progPath\CLSID"

        if ($clsid) {
            $candidatePaths = @(
                "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Classes\CLSID\$clsid\InprocServer32",
                "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Classes\CLSID\$clsid\InprocServer32",
                "Registry::HKEY_CLASSES_ROOT\WOW6432Node\CLSID\$clsid\InprocServer32",
                "Registry::HKEY_CLASSES_ROOT\CLSID\$clsid\InprocServer32"
            )

            foreach ($p in $candidatePaths) {
                if (Test-Path $p) {
                    $inprocPath = $p
                    $dll = Get-RegistryDefaultValue -Path $p
                    break
                }
            }
        }
    }

    [PSCustomObject]@{
        ProgId           = $ProgId
        Exists           = $exists
        CLSID            = $clsid
        InprocRegistered = [bool]$inprocPath
        InprocPath       = $inprocPath
        DllPath          = $dll
    }
}

function Find-ZkemDll {
    param([string]$PreferredPath)

    if ($PreferredPath -and (Test-Path $PreferredPath)) {
        return (Resolve-Path $PreferredPath).Path
    }

    $quickCandidates = @(
        "$PSScriptRoot\zkemkeeper.dll",
        "$PSScriptRoot\tools\zkemkeeper.dll",
        "C:\Windows\SysWOW64\zkemkeeper.dll",
        "C:\Windows\System32\zkemkeeper.dll",
        "C:\Program Files\ZKTeco\zkemkeeper.dll",
        "C:\Program Files (x86)\ZKTeco\zkemkeeper.dll"
    )

    foreach ($candidate in $quickCandidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    if ($SkipSearch) {
        return $null
    }

    $searchRoots = @("$PSScriptRoot", "C:\Program Files", "C:\Program Files (x86)")
    foreach ($root in $searchRoots) {
        if (-not (Test-Path $root)) { continue }
        try {
            $found = Get-ChildItem -Path $root -Filter "zkemkeeper.dll" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($found) {
                return $found.FullName
            }
        }
        catch {
            # Continuar con siguiente raíz
        }
    }

    return $null
}

Write-Host "=== Diagnóstico/Registro ZKTeco COM (zkemkeeper) ===" -ForegroundColor Cyan

if (-not (Test-IsAdmin)) {
    throw "Este script debe ejecutarse como Administrador. Clic derecho -> 'Ejecutar como administrador'."
}

$before = @(
    Get-ProgIdStatus -ProgId 'zkemkeeper.CZKEM'
    Get-ProgIdStatus -ProgId 'zkemkeeper.ZKEM'
    Get-ProgIdStatus -ProgId 'zkemkeeper.ZKEM.1'
)

Write-Host "\nEstado ANTES:" -ForegroundColor Yellow
$before | Format-Table ProgId, Exists, CLSID, InprocRegistered, DllPath -AutoSize

$hasValidCom = $before | Where-Object { $_.InprocRegistered -and $_.DllPath }
if ($hasValidCom) {
    Write-Host "\nOK: Ya existe registro COM funcional de zkemkeeper." -ForegroundColor Green
    return
}

$dll = Find-ZkemDll -PreferredPath $DllPath
if (-not $dll) {
    throw "No encontré 'zkemkeeper.dll'. Instala primero el SDK StandAlone de ZKTeco o pasa -DllPath con la ruta exacta."
}

Write-Host "\nDLL encontrada: $dll" -ForegroundColor Cyan

$regsvr32x86 = Join-Path $env:WINDIR 'SysWOW64\regsvr32.exe'
if (-not (Test-Path $regsvr32x86)) {
    throw "No se encontró regsvr32 x86 en '$regsvr32x86'."
}

Write-Host "Registrando COM x86..." -ForegroundColor Cyan
& $regsvr32x86 /s "$dll"
if ($LASTEXITCODE -ne 0) {
    throw "regsvr32 devolvió código $LASTEXITCODE al registrar '$dll'."
}

$after = @(
    Get-ProgIdStatus -ProgId 'zkemkeeper.CZKEM'
    Get-ProgIdStatus -ProgId 'zkemkeeper.ZKEM'
    Get-ProgIdStatus -ProgId 'zkemkeeper.ZKEM.1'
)

Write-Host "\nEstado DESPUÉS:" -ForegroundColor Yellow
$after | Format-Table ProgId, Exists, CLSID, InprocRegistered, DllPath -AutoSize

$ok = $after | Where-Object { $_.InprocRegistered -and $_.DllPath }
if (-not $ok) {
    throw "No quedó completo el registro COM (faltó InprocServer32). Reinstala SDK ZKTeco x86 y vuelve a ejecutar este script."
}

Write-Host "\nListo: zkemkeeper quedó registrado para uso x86." -ForegroundColor Green
