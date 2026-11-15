# SyncForge

Native Windows-11-App (WinUI 3, .NET 10) zum Synchronisieren von Ordnern in ein oder mehrere Ziele.
Funktionsumfang und Etappenplan: siehe [SPEC.md](SPEC.md).

## Voraussetzungen
- Windows 11
- .NET 10 SDK (`winget install Microsoft.DotNet.SDK.10`)
- Optional: Visual Studio 2026 mit Workload „WinUI-Anwendungsentwicklung“ zum Debuggen

## Bauen & Starten
```powershell
dotnet build SyncForge.sln -c Debug -p:Platform=x64
.\src\SyncForge\bin\x64\Debug\net10.0-windows10.0.26100.0\SyncForge.exe
```

## Tests
```powershell
dotnet test --project tests\SyncForge.Core.Tests\SyncForge.Core.Tests.csproj
```

## MSIX-Paket
```powershell
powershell -File tools\Build-Msix.ps1                       # baut artifacts\msix\...\SyncForge_x.y.z.0_x64.msix
powershell -ExecutionPolicy Bypass -File tools\Install-Msix.ps1   # als Administrator: Testzertifikat, signieren, installieren
```
`Install-Msix.ps1` legt ein selbstsigniertes Zertifikat „CN=SyncForge Dev“ an und vertraut ihm für App-Pakete.
Deinstallieren: `Get-AppxPackage SyncForge | Remove-AppxPackage`. Die installierte App hat eigene Daten (paketierter
AppData-Bereich), getrennt vom unpaketierten Entwicklungs-Build.

## Sync-Modi
| Modus | Verhalten |
|---|---|
| Spiegeln | Ziel = exakte Kopie der Quelle (inkl. Löschungen; optional Papierkorb) |
| Versioniert | wie Spiegeln, überschriebene/gelöschte Dateien landen in `Ziel\.syncforge_versions\` (Seite „Versionen“ zum Wiederherstellen) |
| Zwei-Wege | Änderungen und Löschungen in beide Richtungen; Stand nach dem letzten Lauf in `%LOCALAPPDATA%\SyncForge\state\`; Konflikte: neuere gewinnt / Quelle gewinnt / beide behalten / nur melden |

## Projektstruktur
```
src/SyncForge.Core/      Sync-Logik ohne UI (testbar)
  Models/                Profil, Ziele, Filter-Einstellungen
  Filtering/PathFilter   Ein-/Ausschlussmuster (*.tmp, ordner\, a\**\b)
  Engine/SyncPlanner     Analyse: Quelle vs. Ziel -> Plan (verändert nichts)
  Engine/SyncExecutor    Ausführung: Temp-Datei + Ersetzen, parallel, Abbruch, Papierkorb
  Engine/ProfileRunner   Mehrere Ziele, Erreichbarkeit, Löschschutz
  Storage/ProfileStore   profiles.json
  History/HistoryStore   history.db (SQLite): Läufe, Ergebnisse je Ziel, Datei-Protokoll, Statistiken
  Scheduling/            Zeitplan-Berechnung (Intervall, täglich, Wochentage, Nachholen)
  Versioning/            Versionsordner: verschieben, auflisten, wiederherstellen, aufräumen
  Engine/TwoWayPlanner   Zwei-Wege-Abgleich mit gemeinsamem Stand und Konfliktregeln
  Engine/VolumeResolver  USB-/lokale Ziele über die Volume-ID (auch unter anderem Laufwerksbuchstaben)
tests/SyncForge.Core.Tests/  xUnit-Tests gegen echte Temp-Ordner
src/SyncForge/
  App.xaml(.cs)          Start, DI-Container, Sprache, Fehlerprotokoll
  MainWindow.xaml(.cs)   Titelleiste, Mica, NavigationView, Fenstergröße
  Views/                 Übersicht, Profile, Profil-Editor, Vorschau/Lauf, Historie, Laufdetails, Einstellungen
  Controls/              DailyBarChart (Datenmenge pro Tag)
  ViewModels/            MVVM (CommunityToolkit.Mvvm)
  Program.cs             Einstiegspunkt mit Einzelinstanz-Weiterleitung
  Services/              Einstellungen, Design, Lokalisierung,
                         SyncCoordinator (alle Läufe), AutomationService (Zeitplan, Überwachung, Laufwerke),
                         TrayIcon (Infobereich, Win32), NotificationService, StartupService (Autostart)
  Strings/<Sprache>/     Resources.resw – generiert, nicht von Hand bearbeiten
  Assets/                App-Icon – generiert
tools/
  strings.csv            Alle UI-Texte (Deutsch/Englisch) an einer Stelle
  Generate-Strings.ps1   strings.csv → .resw
  Generate-Icon.ps1      zeichnet AppIcon.png und SyncForge.ico
```

## Texte übersetzen / ergänzen
1. Zeile in `tools/strings.csv` ergänzen (`Schlüssel;Deutsch;Englisch`). Für XAML-Elemente lautet der Schlüssel
   `<x:Uid>.<Eigenschaft>`, z. B. `Settings_Theme.Header`.
2. `powershell -File tools/Generate-Strings.ps1` ausführen.
3. Im Code: `Localization.Get("Schlüssel")`.

## Daten
Einstellungen und Protokolle liegen unter `%LOCALAPPDATA%\SyncForge\`
(`settings.json`, `profiles.json`, `history.db`, `crash.log` bei Fehlern).
