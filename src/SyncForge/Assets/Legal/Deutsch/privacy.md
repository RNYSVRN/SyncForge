# Datenschutzerklärung

> ENTWURF – Vor einer Veröffentlichung (z. B. im Microsoft Store) rechtlich prüfen lassen und Kontaktdaten ergänzen.

Stand: November 2026

## Kurz gesagt

SyncForge arbeitet **vollständig lokal auf deinem PC**. Die App sendet **keine Daten an den Anbieter** oder an Dritte, enthält **keine Telemetrie, keine Werbung und kein Tracking** und benötigt **kein Benutzerkonto**.

## Welche Daten SyncForge verarbeitet

Um Ordner zu synchronisieren, liest und schreibt SyncForge die Dateien und Ordner, die du in deinen Profilen als Quelle und Ziel festlegst. Dateiinhalte werden ausschließlich zwischen diesen Orten kopiert.

Lokal gespeichert werden:

- **Einstellungen** (`settings.json`) – z. B. Design, Sprache, Benachrichtigungen.
- **Backup-Profile** (`profiles.json`) – Pfade von Quellen und Zielen, Filter, Zeitpläne, Seriennummer und Bezeichnung von Ziel-Laufwerken.
- **Historie** (`history.db`) – Zeitpunkt und Ergebnis jedes Laufs sowie die Namen und Pfade der verarbeiteten Dateien.
- **Zwei-Wege-Stand** (Ordner `state`) – Pfade, Größen und Änderungsdaten der zuletzt abgeglichenen Dateien.
- **Fehlerprotokoll** (`crash.log`) – nur bei unerwarteten Fehlern; kann Dateipfade enthalten.

Diese Daten liegen im Ordner `%LOCALAPPDATA%\SyncForge` (bei Installation als App-Paket im geschützten Datenbereich der App) und verlassen deinen PC nicht. Du kannst sie jederzeit über **Einstellungen › Datenordner** einsehen, die Historie in den Einstellungen löschen und alle Daten durch Deinstallation bzw. Löschen des Ordners entfernen.

## Netzwerk und Cloud

SyncForge greift nur auf Netzwerkfreigaben oder Cloud-Ordner zu, die du selbst als Ziel einrichtest. Für Cloud-Ordner gelten die Datenschutzbestimmungen des jeweiligen Cloud-Anbieters, dessen Programm die Dateien weiter überträgt.

## Windows-Funktionen

- **Benachrichtigungen** werden über Windows angezeigt und enthalten Profilnamen und eine Zusammenfassung des Laufs.
- **Autostart** wird – wenn du ihn einschaltest – in Windows eingetragen.
- Bei Bezug über den **Microsoft Store** verarbeitet Microsoft Daten zu Kauf und Installation nach eigenen Datenschutzbestimmungen; darauf hat der Anbieter keinen Einfluss.

## Deine Rechte

Da der Anbieter keine personenbezogenen Daten von dir erhält, liegen keine Daten vor, über die Auskunft erteilt oder die berichtigt oder gelöscht werden könnten. Alle von SyncForge gespeicherten Daten befinden sich unter deiner Kontrolle auf deinem Gerät.

## Kontakt

Ronny Severin
[Kontaktadresse/E-Mail vor Veröffentlichung eintragen]
