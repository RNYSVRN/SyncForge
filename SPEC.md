# SyncForge – Spezifikation

Native Windows-11-App zum Synchronisieren von Dateien und Verzeichnissen in ein oder mehrere Zielverzeichnisse.

## Technik
- **WinUI 3** (Windows App SDK) / C# / **.NET 10 (LTS)**
- MVVM mit CommunityToolkit.Mvvm
- Fluent Design: Mica-Hintergrund, NavigationView, Systemakzentfarbe
- Design: **System (automatisch) / Hell / Dunkel**, umschaltbar in den Einstellungen
- Sprachen: **Deutsch / Englisch** (.resw), Standard = Systemsprache, umschaltbar
- Verteilung: **MSIX-Paket** (während der Entwicklung unpaketiert gestartet)
- Hintergrundbetrieb: **Tray-Icon + Autostart**, Windows-Benachrichtigungen

## Backup-Profile
- Ein Profil = Quelle(n) → ein oder mehrere Ziele
- **Sync-Modi** (pro Profil):
  - Spiegeln (1:1, inkl. Löschungen)
  - Versioniert (überschriebene/gelöschte Dateien in Versionsordner, N Stände)
  - Zwei-Wege-Sync mit Konfliktbehandlung (pro Profil: neuere gewinnt / Quelle gewinnt / beide behalten / nachfragen)
- **Mehrere Ziele**: nacheinander oder parallel (pro Profil); nicht verfügbare Ziele überspringen und nachholen
- **Ziele**: lokale & USB-Laufwerke (Erkennung über Volume-ID), Netzwerk/NAS (UNC, optional Zugangsdaten), Cloud-Ordner (OneDrive/Dropbox/Google Drive lokal)
- **Änderungserkennung**: Größe + Änderungsdatum; Hashvergleich optional pro Profil
- **Auslöser**: manuell, Zeitplan, Echtzeit-Überwachung (FileSystemWatcher + Entprellung), Ereignisse (Anmeldung, Herunterfahren, Laufwerk verfügbar)
- **Filter**: Ein-/Ausschlussmuster, Größe, Datum, versteckte/Systemdateien
- **Sicherheit**: Vorschau/Probelauf, Löschschutz (Schwelle in %, Papierkorb/Versionsordner), Prüfsummen-Verifikation

## Historie
- Laufübersicht (Start, Dauer, Status, Dateien, Datenmenge)
- Datei-Details (durchsuchbar)
- Statistiken/Diagramme
- Wiederherstellen aus Versionen
- Speicherung: SQLite

## Einstellungen
- Design, Sprache, Autostart, Tray-Verhalten
- Benachrichtigungen (Erfolg / Fehler / Konflikt einzeln)
- Leistung (parallele Kopien, Bandbreitenlimit, Pause bei Akku/getakteter Verbindung)
- Import/Export von Profilen & Einstellungen
- Aufbewahrung von Historie und Versionen

## Etappen
1. **Grundgerüst**: Projekt, Navigation (Übersicht, Profile, Historie, Einstellungen), Mica, Theme-Umschaltung, DE/EN
2. **Profile + Sync-Engine** (Spiegeln, Filter, Vorschau, Löschschutz, Fortschritt)
3. **Historie** (SQLite, Laufübersicht, Datei-Details, Statistiken)
4. **Erweitert**: Versionierung + Wiederherstellen, Zwei-Wege-Sync, Zeitplan, Echtzeit-Überwachung, Ereignisse, Tray, Benachrichtigungen, MSIX
