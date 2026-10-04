# Genera en Windows el instalador artifacts\installers\Pos-<v>-win-x64-setup.exe y el portable
# Pos-<v>-win-x64-portable.exe. Requiere el SDK de .NET 10 e Inno Setup 6 (https://jrsoftware.org/isdl.php).
# Uso: powershell -ExecutionPolicy Bypass -File scripts\build-installers.ps1
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $version) { throw 'No se encontró <Version> en Directory.Build.props' }

$publish = Join-Path $root 'artifacts\publish\win-x64'
$out = Join-Path $root 'artifacts\installers'
New-Item -ItemType Directory -Force -Path $out | Out-Null
if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }

Write-Host '==> dotnet publish (win-x64)'
dotnet publish (Join-Path $root 'src\Pos.Desktop') -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Falló dotnet publish' }

Copy-Item (Join-Path $publish 'Pos.exe') (Join-Path $out "Pos-$version-win-x64-portable.exe")

$iscc = (Get-Command iscc -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    $iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
              "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") |
        Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc) { throw 'No se encontró Inno Setup 6. Instálalo desde https://jrsoftware.org/isdl.php' }

Write-Host '==> Inno Setup'
& $iscc /Q "/DAppVersion=$version" "/DSourceDir=$publish" "/DOutputDir=$out" `
    (Join-Path $root 'packaging\windows\Pos.iss')
if ($LASTEXITCODE -ne 0) { throw 'Falló Inno Setup' }

Write-Host "Listo. Paquetes en ${out}:"
Get-ChildItem $out | Format-Table Name, Length
