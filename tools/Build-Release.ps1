# Baut ein Release für GitHub nach artifacts\release\:
#   SyncForge-<Version>-x64-Setup.exe   Installationsprogramm (pro Benutzer, ohne Administratorrechte)
#   SyncForge-<Version>-x64.zip         ohne Installation (entpacken und SyncForge.exe starten)
#   SHA256SUMS.txt                      Prüfsummen zum Abgleichen
#   winget\                             Manifest für winget
#
# Aufruf: powershell -ExecutionPolicy Bypass -File tools\Build-Release.ps1 [-GitHubRepo besitzer/name]
# Voraussetzung: Inno Setup 6 (winget install JRSoftware.InnoSetup)
param(
    # GitHub-Repository "besitzer/name": Damit wird das winget-Manifest mit den Download-Adressen erzeugt.
    [string]$GitHubRepo = 'RNYSVRN/SyncForge'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\SyncForge\SyncForge.csproj'
$version = ([xml](Get-Content $project -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw 'Keine <Version> in SyncForge.csproj gefunden.' }

$publish = Join-Path $root 'artifacts\publish\x64'
$release = Join-Path $root 'artifacts\release'
foreach ($dir in $publish, $release) { if (Test-Path $dir) { Remove-Item $dir -Recurse -Force } }
New-Item -ItemType Directory -Force $release | Out-Null

Write-Host "SyncForge $version wird veröffentlicht ..."
dotnet publish $project -c Release -p:Platform=x64 -r win-x64 --self-contained true -o $publish -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Veröffentlichen fehlgeschlagen ($LASTEXITCODE)" }
if (-not (Test-Path (Join-Path $publish 'SyncForge.pri'))) { throw 'SyncForge.pri fehlt im Ausgabeordner (die App würde nicht starten).' }
Get-ChildItem $publish -Filter *.pdb -Recurse | Remove-Item

# ZIP ohne Installation
$zip = Join-Path $release "SyncForge-$version-x64.zip"
Write-Host 'ZIP wird erstellt ...'
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -CompressionLevel Optimal

# Installationsprogramm
$iscc = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { $iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source }
if (-not $iscc) { throw 'Inno Setup 6 nicht gefunden (winget install JRSoftware.InnoSetup).' }
Write-Host 'Installationsprogramm wird erstellt ...'
& $iscc /Q "/DAppVersion=$version" "/DPublishDir=$publish" "/DOutputDir=$release" (Join-Path $root 'installer\SyncForge.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup fehlgeschlagen ($LASTEXITCODE)" }
$setup = Join-Path $release "SyncForge-$version-x64-Setup.exe"

# Prüfsummen
$sums = foreach ($file in $setup, $zip) {
    '{0}  {1}' -f (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path $file -Leaf)
}
[IO.File]::WriteAllLines((Join-Path $release 'SHA256SUMS.txt'), [string[]]$sums)

# winget-Manifest (Einreichen: siehe README, Abschnitt "Veröffentlichen")
if ($GitHubRepo) {
    $id = 'RonnySeverin.SyncForge'
    $url = "https://github.com/$GitHubRepo/releases/download/v$version/SyncForge-$version-x64-Setup.exe"
    $hash = (Get-FileHash $setup -Algorithm SHA256).Hash
    $dir = Join-Path $release "winget\manifests\r\RonnySeverin\SyncForge\$version"
    New-Item -ItemType Directory -Force $dir | Out-Null
    $templates = Join-Path $root 'installer\winget'
    foreach ($template in Get-ChildItem $templates -Filter *.yaml) {
        $text = [IO.File]::ReadAllText($template.FullName)
        $text = $text.Replace('{ID}', $id).Replace('{VERSION}', $version).Replace('{URL}', $url).Replace('{SHA256}', $hash).Replace('{REPO}', $GitHubRepo)
        $name = $template.Name.Replace('template', $id)
        [IO.File]::WriteAllText((Join-Path $dir $name), $text, [Text.UTF8Encoding]::new($false))
    }
}

Write-Host ''
Get-ChildItem $release -File | ForEach-Object { Write-Host ('{0,-36} {1,8:N1} MB' -f $_.Name, ($_.Length / 1MB)) }
if (-not $GitHubRepo) { Write-Host 'Hinweis: Mit -GitHubRepo besitzer/name wird zusätzlich das winget-Manifest erzeugt.' }
