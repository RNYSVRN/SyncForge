# Builds the SyncForge installer:
#   1. Publish the app self-contained (includes .NET and the Windows App SDK, nothing to install on the target PC)
#   2. Create the installer with Inno Setup -> dist\SyncForge-Setup-<version>.exe
#
# Run from the repository root:  powershell -ExecutionPolicy Bypass -File installer\build.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root 'dist\publish'

Write-Host '1/2  Publishing app ...'
if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }
dotnet publish (Join-Path $root 'src\SyncForge\SyncForge.csproj') -c Release -p:Platform=x64 -r win-x64 --self-contained true -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
# Without the resource file (texts, XAML) the app would not start
if (-not (Test-Path (Join-Path $publish 'SyncForge.pri'))) { throw 'SyncForge.pri is missing in the publish folder' }

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 (ISCC.exe) not found. Download: https://jrsoftware.org/isdl.php' }

Write-Host '2/2  Building installer ...'
& $iscc (Join-Path $PSScriptRoot 'SyncForge.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed' }

Get-ChildItem (Join-Path $root 'dist') -Filter 'SyncForge-Setup-*.exe' | Sort-Object LastWriteTime | Select-Object -Last 1 |
    ForEach-Object { Write-Host ("Done: {0}  ({1:N1} MB)" -f $_.FullName, ($_.Length / 1MB)) }
