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

## Projektstruktur
```
src/SyncForge/
  App.xaml(.cs)          Start, DI-Container, Sprache, Fehlerprotokoll
  MainWindow.xaml(.cs)   Titelleiste, Mica, NavigationView, Fenstergröße
  Views/                 Übersicht, Profile, Historie, Einstellungen
  ViewModels/            MVVM (CommunityToolkit.Mvvm)
  Services/              Einstellungen (JSON), Design, Lokalisierung
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
(`settings.json`, `crash.log` bei Fehlern).
