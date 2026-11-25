# Sammelt die Lizenzen aller mitgelieferten Drittanbieter-Komponenten nach src\SyncForge\Assets\Licenses:
#   licenses.json  – Liste für die Seite "Lizenzen & Rechtliches"
#   <id>.txt       – vollständiger Lizenztext je Komponente (plus Drittanbieter-Hinweise, falls vorhanden)
# Versionen kommen aus obj\project.assets.json (also den tatsächlich verwendeten Paketen),
# Lizenztexte aus den NuGet-Paketen bzw. tools\license-templates.
# Nach Paket-Updates erneut ausführen:  powershell -File tools\Collect-Licenses.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$nuget = Join-Path $env:USERPROFILE '.nuget\packages'
$templates = Join-Path $PSScriptRoot 'license-templates'
$out = Join-Path $root 'src\SyncForge\Assets\Licenses'
New-Item -ItemType Directory -Force $out | Out-Null

$assetsFile = Join-Path $root 'src\SyncForge\obj\project.assets.json'
if (-not (Test-Path $assetsFile)) { throw 'Bitte zuerst bauen (dotnet build), damit obj\project.assets.json existiert.' }
$assets = Get-Content $assetsFile -Raw -Encoding UTF8 | ConvertFrom-Json
$versions = @{}
foreach ($lib in $assets.libraries.PSObject.Properties) {
    if ($lib.Value.type -ne 'package') { continue }
    $name, $ver = $lib.Name -split '/'
    $versions[$name.ToLowerInvariant()] = $ver
}

# Pakete, die im Projekt mit ExcludeAssets="all" ausgeschlossen sind, werden nicht mitgeliefert.
$csproj = [xml](Get-Content (Join-Path $root 'src\SyncForge\SyncForge.csproj') -Raw -Encoding UTF8)
foreach ($ref in $csproj.Project.ItemGroup.PackageReference) {
    if ($ref.ExcludeAssets -eq 'all') { $versions.Remove($ref.Include.ToLowerInvariant()) }
}

function Read-Text([string]$path) { [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8).Trim() }
function Package-File([string]$id, [string]$pattern) {
    $dir = Join-Path $nuget "$id\$($versions[$id])"
    Get-ChildItem $dir -File -ErrorAction SilentlyContinue | Where-Object Name -match $pattern | Select-Object -First 1
}

# Komponenten (zusammengefasste Pakete). Reine Build-Werkzeuge (SDK-BuildTools) werden nicht mitgeliefert.
$components = @(
    @{ Id = 'windowsappsdk'; Name = 'Windows App SDK / WinUI 3'; Main = 'microsoft.windowsappsdk'; Prefix = 'microsoft.windowsappsdk'
       Authors = 'Microsoft Corporation'; License = 'Microsoft Software License Terms'; Url = 'https://github.com/microsoft/WindowsAppSDK'
       Files = @('^license\.txt$', '^NOTICE\.txt$') },
    @{ Id = 'webview2'; Name = 'Microsoft Edge WebView2 SDK'; Main = 'microsoft.web.webview2'
       Authors = 'Microsoft Corporation'; License = 'Microsoft Software License Terms'; Url = 'https://aka.ms/webview2'
       Files = @('^LICENSE\.txt$', '^NOTICE\.txt$') },
    @{ Id = 'windows-ai-ml'; Name = 'Windows AI Machine Learning'; Main = 'microsoft.windows.ai.machinelearning'
       Authors = 'Microsoft Corporation'; License = 'Microsoft Software License Terms'; Url = 'https://aka.ms/windowsappsdk'
       Files = @('^license\.txt$', '^ThirdPartyNotices\.txt$') },
    @{ Id = 'dotnet-community-toolkit'; Name = '.NET Community Toolkit (MVVM)'; Main = 'communitytoolkit.mvvm'; Prefix = 'communitytoolkit.common|communitytoolkit.mvvm'
       Authors = '.NET Foundation and Contributors'; License = 'MIT'; Url = 'https://github.com/CommunityToolkit/dotnet'
       Files = @('^License\.md$', '^ThirdPartyNotices\.txt$') },
    @{ Id = 'windows-community-toolkit'; Name = 'Windows Community Toolkit'; Main = 'communitytoolkit.winui.controls.settingscontrols'; Prefix = 'communitytoolkit.winui'
       Authors = '.NET Foundation and Contributors'; License = 'MIT'; Url = 'https://github.com/CommunityToolkit/Windows'
       Files = @('^License\.md$') },
    @{ Id = 'dependency-injection'; Name = 'Microsoft.Extensions.DependencyInjection'; Main = 'microsoft.extensions.dependencyinjection'; Prefix = 'microsoft.extensions.dependencyinjection'
       Authors = 'Microsoft Corporation'; License = 'MIT'; Url = 'https://github.com/dotnet/runtime'
       Template = 'MIT.txt'; Copyright = 'Copyright (c) .NET Foundation and Contributors'; Files = @('^THIRD-PARTY-NOTICES\.TXT$') },
    @{ Id = 'microsoft-data-sqlite'; Name = 'Microsoft.Data.Sqlite'; Main = 'microsoft.data.sqlite'; Prefix = 'microsoft.data.sqlite'
       Authors = 'Microsoft Corporation'; License = 'MIT'; Url = 'https://github.com/dotnet/efcore'
       Template = 'MIT.txt'; Copyright = 'Copyright (c) .NET Foundation and Contributors' },
    @{ Id = 'sqlitepclraw'; Name = 'SQLitePCLRaw'; Main = 'sqlitepclraw.core'; Prefix = 'sqlitepclraw'
       Authors = 'Eric Sink, SourceGear LLC'; License = 'Apache-2.0'; Url = 'https://github.com/ericsink/SQLitePCL.raw'
       Template = 'Apache-2.0.txt'; Copyright = 'Copyright 2014-2024 SourceGear, LLC' },
    @{ Id = 'sqlite'; Name = 'SQLite'; Main = 'sqlitepclraw.lib.e_sqlite3'
       Authors = 'D. Richard Hipp and contributors'; License = 'Public Domain'; Url = 'https://www.sqlite.org'
       Template = 'SQLite-PublicDomain.txt'; Version = 'runtime' },  # SQLite-Version ermittelt die App
    @{ Id = 'system-management'; Name = 'System.Management'; Main = 'system.management'; Prefix = 'system.management|system.codedom'
       Authors = 'Microsoft Corporation'; License = 'MIT'; Url = 'https://github.com/dotnet/runtime'
       Template = 'MIT.txt'; Copyright = 'Copyright (c) .NET Foundation and Contributors'; Files = @('^THIRD-PARTY-NOTICES\.TXT$') },
    @{ Id = 'system-numerics-tensors'; Name = 'System.Numerics.Tensors'; Main = 'system.numerics.tensors'
       Authors = 'Microsoft Corporation'; License = 'MIT'; Url = 'https://github.com/dotnet/runtime'
       Files = @('^LICENSE\.TXT$', '^THIRD-PARTY-NOTICES\.TXT$') },
    @{ Id = 'dotnet'; Name = '.NET Runtime'; Main = $null
       Authors = '.NET Foundation and Contributors'; License = 'MIT'; Url = 'https://github.com/dotnet/runtime'
       Template = 'MIT.txt'; Copyright = 'Copyright (c) .NET Foundation and Contributors'; Version = 'runtime' }  # ermittelt die App zur Laufzeit
)

$list = foreach ($c in $components) {
    $version = if ($c.Version) { $c.Version } elseif ($versions.ContainsKey($c.Main)) { $versions[$c.Main] } else { $null }
    if ($c.Main -and -not $versions.ContainsKey($c.Main)) {
        Write-Warning "$($c.Name): Paket $($c.Main) wird nicht verwendet, übersprungen"
        Remove-Item (Join-Path $out "$($c.Id).txt") -ErrorAction SilentlyContinue
        continue
    }

    $parts = @()
    if ($c.Template) {
        $parts += (Read-Text (Join-Path $templates $c.Template)).Replace('{COPYRIGHT}', $c.Copyright)
    }
    foreach ($pattern in @($c.Files)) {
        if (-not $pattern) { continue }
        $file = Package-File $c.Main $pattern
        if ($file) { $parts += Read-Text $file.FullName } else { Write-Warning "$($c.Name): Datei $pattern fehlt" }
    }
    $separator = "`r`n`r`n" + ('=' * 78) + "`r`n`r`n"
    [IO.File]::WriteAllText((Join-Path $out "$($c.Id).txt"), ($parts -join $separator), [Text.UTF8Encoding]::new($false))

    # ArrayList: wird von ConvertTo-Json (PowerShell 5.1) auch mit nur einem Eintrag als Liste geschrieben
    $packages = [System.Collections.ArrayList]::new()
    if ($c.Prefix) { $versions.Keys | Where-Object { $_ -match "^($($c.Prefix))" } | Sort-Object | ForEach-Object { [void]$packages.Add($_) } }
    elseif ($c.Main) { [void]$packages.Add($c.Main) }
    [ordered]@{
        id = $c.Id; name = $c.Name; version = $version; authors = $c.Authors; license = $c.License
        url = $c.Url; file = "$($c.Id).txt"; packages = $packages
    }
}

$json = ConvertTo-Json @($list) -Depth 4
[IO.File]::WriteAllText((Join-Path $out 'licenses.json'), $json, [Text.UTF8Encoding]::new($false))
Write-Host "$(@($list).Count) Komponenten nach $out geschrieben"
