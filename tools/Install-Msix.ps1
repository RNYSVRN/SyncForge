# Signiert das zuletzt gebaute MSIX-Paket mit einem selbstsignierten Testzertifikat und installiert es.
#
# Was dieses Skript an deinem System ändert:
#   1. Legt (falls noch nicht vorhanden) das Zertifikat "CN=SyncForge Dev" in deinem Benutzerspeicher an.
#   2. Vertraut diesem Zertifikat für App-Pakete (Zertifikatspeicher "Vertrauenswürdige Personen" des
#      Computers) – dafür sind Administratorrechte nötig.
#   3. Installiert SyncForge als App (erscheint im Startmenü, deinstallierbar über Einstellungen > Apps).
#
# Aufruf in einer PowerShell "Als Administrator ausführen":
#   powershell -ExecutionPolicy Bypass -File tools\Install-Msix.ps1
#
# Deinstallieren:  Get-AppxPackage SyncForge | Remove-AppxPackage
# Zertifikat entfernen: certlm.msc > Vertrauenswürdige Personen > "SyncForge Dev" löschen
param([ValidateSet('x64', 'ARM64')][string]$Platform = 'x64')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$subject = 'CN=SyncForge Dev' # muss dem Publisher in Package.appxmanifest entsprechen

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { throw 'Bitte PowerShell "Als Administrator ausführen" und das Skript erneut starten.' }

$msix = Get-ChildItem (Join-Path $root 'artifacts\msix') -Recurse -Filter "*_$Platform.msix" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $msix) { throw 'Kein Paket gefunden – zuerst tools\Build-Msix.ps1 ausführen.' }
Write-Host "Paket: $($msix.FullName)"

# 1. Zertifikat
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $subject -and $_.NotAfter -gt (Get-Date) } | Select-Object -First 1
if (-not $cert) {
    Write-Host 'Erzeuge Testzertifikat ...'
    $cert = New-SelfSignedCertificate -Type Custom -Subject $subject -KeyUsage DigitalSignature -FriendlyName 'SyncForge Dev (Test)' `
        -CertStoreLocation 'Cert:\CurrentUser\My' -NotAfter (Get-Date).AddYears(3) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
}

# 2. Signieren (signtool aus dem Windows-SDK-NuGet-Paket)
$signtool = Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $signtool) { throw 'signtool.exe nicht gefunden (NuGet-Paket Microsoft.Windows.SDK.BuildTools).' }
& $signtool.FullName sign /fd SHA256 /sha1 $cert.Thumbprint /s My $msix.FullName
if ($LASTEXITCODE -ne 0) { throw "Signieren fehlgeschlagen ($LASTEXITCODE)" }

# 3. Zertifikat vertrauen
$cer = Join-Path $env:TEMP 'SyncForgeDev.cer'
Export-Certificate -Cert $cert -FilePath $cer | Out-Null
Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
Remove-Item $cer

# 4. Installieren (vorhandene Version wird aktualisiert)
Add-AppxPackage -Path $msix.FullName -ForceUpdateFromAnyVersion
Write-Host ''
Write-Host 'SyncForge ist installiert und im Startmenü zu finden.'
