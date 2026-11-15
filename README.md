# SyncForge

**English** · [Deutsch](README.de.md)

[![Build](https://github.com/RNYSVRN/SyncForge/actions/workflows/build.yml/badge.svg)](https://github.com/RNYSVRN/SyncForge/actions/workflows/build.yml)

A native Windows 11 app (WinUI 3, .NET 10) to sync and back up folders to one or more destinations:
local drives, USB drives, network shares (NAS) and cloud folders.
Free and open source under the [MIT license](LICENSE), © 2026 Ronny Severin.

## Features
- **Backup profiles** with one source and several destinations (one after another or at the same time)
- **Modes:** mirror, versioned (restore older versions) and two-way with conflict rules
- **Automatic:** schedules, real-time watching, on SyncForge start and when a destination drive is connected
- **Safe:** preview before every run, deletion protection, Recycle Bin, optional checksums, never half-copied files
- **Filters** for files and folders, size limits, online-only cloud files are not downloaded
- **History** with statistics and details for every file
- Notification area, start with Windows, notifications, light and dark theme, English and German

## Installation
Download the latest version from [Releases](https://github.com/RNYSVRN/SyncForge/releases):
- `SyncForge-x.y.z-x64-Setup.exe`: installer, no administrator rights needed.
- `SyncForge-x.y.z-x64.zip`: no installation, unzip and run `SyncForge.exe`.

Requires Windows 11 (x64). The files are not code-signed, so Windows SmartScreen shows a notice on first launch:
choose **More info** and then **Run anyway**. Every release lists the SHA-256 checksums.

## Privacy
SyncForge works entirely locally: no telemetry, no ads, no account. The only exception is the optional update check,
which is off by default and asks GitHub once a day whether a new version exists.

## Support
SyncForge is free and will stay that way. If it saves you time and nerves, I'd be happy about a coffee:
[ko-fi.com/rnysvrn](https://ko-fi.com/rnysvrn). A star on GitHub and feedback as an
[issue](https://github.com/RNYSVRN/SyncForge/issues) help as well.

---

# Development
Feature scope and stage plan (German): see [SPEC.md](SPEC.md).

## Requirements
- Windows 11
- .NET 10 SDK (`winget install Microsoft.DotNet.SDK.10`)
- Optional: Visual Studio 2026 with the "WinUI application development" workload for debugging

## Build & run
```powershell
dotnet build SyncForge.sln -c Debug -p:Platform=x64
.\src\SyncForge\bin\x64\Debug\net10.0-windows10.0.26100.0\SyncForge.exe
```

## Tests
```powershell
dotnet test --project tests\SyncForge.Core.Tests\SyncForge.Core.Tests.csproj
```
The load test with 100,000 files runs only on demand:
```powershell
dotnet test --project tests\SyncForge.Core.Tests\SyncForge.Core.Tests.csproj -- --filter-class "*LoadTests" --explicit only
```

## Releases (GitHub, winget)
GitHub Actions (`.github/workflows/build.yml`):
- Every push to `main` and every pull request: tests and build.
- Pushing a tag `vX.Y.Z` (must match `<Version>` in `src/SyncForge/SyncForge.csproj`), e.g.
  `git tag v0.5.0` and `git push origin v0.5.0`: the action builds the installer, ZIP and checksums, creates a
  **release draft** and uploads the winget manifest as an artifact. Add the release notes on GitHub and publish the draft.

Locally (requires Inno Setup 6: `winget install JRSoftware.InnoSetup`):
```powershell
powershell -ExecutionPolicy Bypass -File tools\Build-Release.ps1
```
Output in `artifacts\release\`:
- `SyncForge-x.y.z-x64-Setup.exe`: per-user installer (no admin rights) into `%LOCALAPPDATA%\Programs\SyncForge`.
  Stops a running SyncForge before updating or uninstalling. Uninstalling removes the startup entry and the
  notification registration and asks whether settings, profiles and history should be deleted too (silent: never).
- `SyncForge-x.y.z-x64.zip`: portable, no installation.
- `SHA256SUMS.txt`: checksums for the release notes.
- `winget\manifests\...`: winget manifest (templates in `installer\winget`), check with `winget validate --manifest <folder>`.

winget: submit the folder `winget\manifests\r\RonnySeverin\SyncForge\x.y.z` as a pull request to
[microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) under the same path (or use `wingetcreate`).
Afterwards: `winget install RonnySeverin.SyncForge`.

Only what SyncForge needs is shipped: the AI, ML, search and widget parts of the Windows App SDK are excluded in the
project file with `ExcludeAssets="all"` (saves about 60 MB). A custom build step copies the resource file
`SyncForge.pri`, without it the unpackaged app does not start.

## MSIX package
```powershell
powershell -File tools\Build-Msix.ps1                             # builds artifacts\msix\...\SyncForge_x.y.z.0_x64.msix
powershell -ExecutionPolicy Bypass -File tools\Install-Msix.ps1   # as administrator: test certificate, sign, install
```
`Install-Msix.ps1` creates a self-signed certificate "CN=SyncForge Dev" and trusts it for app packages.
Uninstall: `Get-AppxPackage SyncForge | Remove-AppxPackage`. The installed app has its own data (packaged AppData
area), separate from the unpackaged development build.

## Help, legal texts and licenses
- Help: `src/SyncForge/Assets/Help/Deutsch|English/*.md`, one topic per file, ordered by file name,
  first line `<!-- glyph:E7C1 -->` = icon (Segoe Fluent Icons). Supported: `#`/`##`/`###`, paragraphs, `-`/`1.` lists,
  `> note`, `**bold**`, `` `code` ``.
- License page and privacy statement: `src/SyncForge/Assets/Legal/Deutsch|English/license.md` and `privacy.md`.
- Third-party licenses: run `powershell -File tools\Collect-Licenses.ps1` after every package update
  (reads the package versions used by the build and takes the license texts from the NuGet packages).

## Sync modes
| Mode | Behavior |
|---|---|
| Mirror | Destination = exact copy of the source (including deletions, optionally to the Recycle Bin) |
| Versioned | Like mirror, overwritten or deleted files go to `Destination\.syncforge_versions\` ("Versions" page to restore) |
| Two-way | Changes and deletions in both directions, state after the last run in `%LOCALAPPDATA%\SyncForge\state\`. Conflicts: newer wins / source wins / keep both / report only |

## Project structure
```
src/SyncForge.Core/      Sync logic without UI (testable)
  Models/                Profile, destinations, filter settings
  Filtering/PathFilter   Include/exclude patterns (*.tmp, folder\, a\**\b)
  Engine/SyncPlanner     Analysis: source vs. destination -> plan (changes nothing)
  Engine/SyncExecutor    Execution: temp file + replace, parallel, cancel, Recycle Bin
  Engine/ProfileRunner   Multiple destinations, reachability, deletion protection
  Engine/TwoWayPlanner   Two-way sync with shared state and conflict rules
  Engine/VolumeResolver  USB/local destinations via volume ID (also under another drive letter)
  Storage/ProfileStore   profiles.json
  History/HistoryStore   history.db (SQLite): runs, results per destination, file log, statistics
  Scheduling/            Schedule calculation (interval, daily, weekdays, catch-up)
  Versioning/            Versions folder: move, list, restore, clean up
  Updates/               Optional update check (GitHub Releases)
tests/SyncForge.Core.Tests/  xUnit tests against real temp folders
src/SyncForge/
  App.xaml(.cs)          Startup, DI container, language, error log
  MainWindow.xaml(.cs)   Title bar, Mica, NavigationView, window size
  Views/                 Overview, profiles, profile editor, preview/run, history, run details, settings, help, legal
  Controls/              DailyBarChart, MarkdownView, CoffeeSteam
  ViewModels/            MVVM (CommunityToolkit.Mvvm)
  Program.cs             Entry point with single-instance redirection
  Services/              Settings, theme, localization, SyncCoordinator (all runs),
                         AutomationService (schedules, watching, drives), TrayIcon (Win32),
                         NotificationService, StartupService, UpdateService
  Strings/<language>/    Resources.resw (generated, do not edit by hand)
  Assets/                App icon, help, legal texts, licenses
installer/               Inno Setup script and winget templates
tools/
  strings.csv            All UI texts (German/English) in one place
  Generate-Strings.ps1   strings.csv -> .resw
  Generate-Icon.ps1      draws AppIcon.png and SyncForge.ico
  Build-Release.ps1      installer, ZIP, checksums, winget manifest
```

## Translating / adding texts
1. Add a line to `tools/strings.csv` (`Key;German;English`, no semicolons inside the texts). For XAML elements the key
   is `<x:Uid>.<Property>`, e.g. `Settings_Theme.Header`.
2. Run `powershell -File tools/Generate-Strings.ps1`.
3. In code: `Localization.Get("Key")`.

## Data
Settings and logs are stored in `%LOCALAPPDATA%\SyncForge\`
(`settings.json`, `profiles.json`, `history.db`, `state\`, `crash.log` after errors).
