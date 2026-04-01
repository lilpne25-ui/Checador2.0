param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDir,
    [Parameter(Mandatory = $true)]
    [string]$OutFile
)

if (!(Test-Path $SourceDir)) {
    throw "No existe carpeta fuente: $SourceDir"
}

$sourceDir = (Resolve-Path $SourceDir).Path
$allDirs = Get-ChildItem -Path $sourceDir -Directory -Recurse | Sort-Object FullName

$dirId = @{}
$dirId[$sourceDir] = 'INSTALLFOLDER'
$idx = 1
foreach ($d in $allDirs) {
    $dirId[$d.FullName] = "DIR$idx"
    $idx++
}

$children = @{}
foreach ($d in $allDirs) {
    $parent = $d.Parent.FullName
    if (-not $children.ContainsKey($parent)) {
        $children[$parent] = New-Object System.Collections.ArrayList
    }
    [void]$children[$parent].Add($d.FullName)
}

$filesByDir = @{}
$allFiles = Get-ChildItem -Path $sourceDir -File -Recurse | Sort-Object FullName
foreach ($f in $allFiles) {
    $parent = $f.Directory.FullName
    if (-not $filesByDir.ContainsKey($parent)) {
        $filesByDir[$parent] = New-Object System.Collections.ArrayList
    }
    [void]$filesByDir[$parent].Add($f.FullName)
}

$sb = New-Object System.Text.StringBuilder
$componentRefs = New-Object System.Collections.Generic.List[string]
$componentIndex = 0

function Emit-Directory {
    param(
        [string]$Path,
        [int]$Indent
    )

    $ind = ' ' * $Indent
    $id = $dirId[$Path]

    if ($Path -eq $sourceDir) {
        [void]$sb.AppendLine("$ind<Directory Id=""$id"" Name=""Checador RH"">")
    }
    else {
        $name = [System.IO.Path]::GetFileName($Path)
        [void]$sb.AppendLine("$ind<Directory Id=""$id"" Name=""$name"">")
    }

    if ($filesByDir.ContainsKey($Path)) {
        foreach ($fp in $filesByDir[$Path]) {
            $script:componentIndex++
            $cmp = "CMP$($script:componentIndex)"
            $fil = "FIL$($script:componentIndex)"
            [void]$sb.AppendLine("$ind  <Component Id=""$cmp"" Guid=""*"">")
            [void]$sb.AppendLine("$ind    <File Id=""$fil"" Source=""$fp"" KeyPath=""yes"" />")
            [void]$sb.AppendLine("$ind  </Component>")
            $componentRefs.Add($cmp) | Out-Null
        }
    }

    if ($children.ContainsKey($Path)) {
        foreach ($child in ($children[$Path] | Sort-Object)) {
            Emit-Directory -Path $child -Indent ($Indent + 2)
        }
    }

    [void]$sb.AppendLine("$ind</Directory>")
}

[void]$sb.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
[void]$sb.AppendLine('  <Package Name="Checador RH" Manufacturer="Operadora Comercial Innovax" Version="2.0.0.0" UpgradeCode="9C8D5A9D-8E63-4C4E-8F64-0B1F6D0E5A13" Language="3082" Scope="perUser">')
[void]$sb.AppendLine('    <MajorUpgrade DowngradeErrorMessage="Ya hay una versión más reciente de Checador RH instalada." />')
[void]$sb.AppendLine('    <MediaTemplate EmbedCab="yes" />')
[void]$sb.AppendLine('    <StandardDirectory Id="LocalAppDataFolder">')

Emit-Directory -Path $sourceDir -Indent 6

[void]$sb.AppendLine('    </StandardDirectory>')
[void]$sb.AppendLine("    <Icon Id=""AppIcon"" SourceFile=""$sourceDir\Checador.Desktop.exe"" />")
[void]$sb.AppendLine('    <Property Id="ARPPRODUCTICON" Value="AppIcon" />')
[void]$sb.AppendLine('    <Feature Id="MainFeature" Title="Checador RH" Level="1">')
foreach ($r in $componentRefs) {
    [void]$sb.AppendLine("      <ComponentRef Id=""$r"" />")
}
[void]$sb.AppendLine('    </Feature>')
[void]$sb.AppendLine('  </Package>')
[void]$sb.AppendLine('</Wix>')

[System.IO.File]::WriteAllText($OutFile, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Output "Generated WXS: $OutFile"
Write-Output "Components: $componentIndex"
