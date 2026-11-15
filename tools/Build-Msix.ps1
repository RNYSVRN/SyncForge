# Baut das MSIX-Paket (unsigniert) nach artifacts\msix\.
# Aufruf: powershell -File tools\Build-Msix.ps1 [-Platform x64|ARM64]
param([ValidateSet('x64', 'ARM64')][string]$Platform = 'x64')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$rid = if ($Platform -eq 'ARM64') { 'win-arm64' } else { 'win-x64' }

dotnet publish (Join-Path $root 'src\SyncForge\SyncForge.csproj') -c Release -p:Platform=$Platform -p:Packaged=true -r $rid -nologo
if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen ($LASTEXITCODE)" }

$msix = Get-ChildItem (Join-Path $root 'artifacts\msix') -Recurse -Filter "*_$Platform.msix" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host ""
Write-Host "Paket: $($msix.FullName)"
Write-Host "Installieren (als Administrator): powershell -ExecutionPolicy Bypass -File tools\Install-Msix.ps1"
