# Datenschutzerklärung

Stand: November 2026

## Kurz gesagt

SyncForge arbeitet **vollständig lokal auf deinem PC**. Die App sendet **keine Daten an den Anbieter** oder an Dritte, enthält **keine Telemetrie, keine Werbung und kein Tracking** und benötigt **kein Benutzerkonto**.

Einzige Ausnahme ist die **Update-Prüfung**, die standardmäßig ausgeschaltet ist. Schaltest du sie ein, fragt SyncForge einmal täglich bei GitHub nach einer neuen Version (siehe unten).

## Welche Daten SyncForge verarbeitet

Um Ordner zu synchronisieren, liest und schreibt SyncForge die Dateien und Ordner, die du in deinen Profilen als Quelle und Ziel festlegst. Dateiinhalte werden ausschließlich zwischen diesen Orten kopiert.

Lokal gespeichert werden:

- **Einstellungen** (`settings.json`): z. B. Design, Sprache, Benachrichtigungen.
- **Backup-Profile** (`profiles.json`): Pfade von Quellen und Zielen, Filter, Zeitpläne, Seriennummer und Bezeichnung von Ziel-Laufwerken.
- **Historie** (`history.db`): Zeitpunkt und Ergebnis jedes Laufs sowie die Namen und Pfade der verarbeiteten Dateien.
- **Zwei-Wege-Stand** (Ordner `state`): Pfade, Größen und Änderungsdaten der zuletzt abgeglichenen Dateien.
- **Fehlerprotokoll** (`crash.log`): nur bei unerwarteten Fehlern; kann Dateipfade enthalten.

Diese Daten liegen im Ordner `%LOCALAPPDATA%\SyncForge` (bei Installation als App-Paket im geschützten Datenbereich der App) und verlassen deinen PC nicht. Du kannst sie jederzeit über **Einstellungen › Datenordner** einsehen, die Historie in den Einstellungen löschen und alle Daten durch Deinstallation bzw. Löschen des Ordners entfernen.

## Netzwerk und Cloud

SyncForge greift nur auf Netzwerkfreigaben oder Cloud-Ordner zu, die du selbst als Ziel einrichtest. Für Cloud-Ordner gelten die Datenschutzbestimmungen des jeweiligen Cloud-Anbieters, dessen Programm die Dateien weiter überträgt.

## Update-Prüfung (optional)

Unter **Einstellungen › Nach Updates suchen** kannst du die Update-Prüfung einschalten. Sie ist standardmäßig aus. Eingeschaltet ruft SyncForge höchstens einmal täglich sowie auf Klick auf **Jetzt suchen** die öffentliche Schnittstelle von GitHub (`api.github.com`) auf, um die Nummer der neuesten Version abzufragen.

Dabei erhält GitHub (GitHub, Inc., ein Unternehmen von Microsoft, USA) technisch bedingt deine **IP-Adresse**, den Zeitpunkt der Anfrage und die **Versionsnummer von SyncForge** (als Kennung der Anfrage). Weitere Daten, etwa über deine Dateien, Profile oder dein Gerät, werden nicht übertragen. Der Anbieter von SyncForge erhält keine Daten aus dieser Anfrage. Für die Verarbeitung bei GitHub gilt die Datenschutzerklärung von GitHub.

Wird eine neue Version gefunden, zeigt SyncForge einen Hinweis mit einem Link zur Download-Seite. Heruntergeladen oder installiert wird nichts automatisch. Ausschalten kannst du die Prüfung jederzeit an derselben Stelle.

## Windows-Funktionen

- **Benachrichtigungen** werden über Windows angezeigt und enthalten Profilnamen und eine Zusammenfassung des Laufs.
- **Autostart** wird in Windows eingetragen, wenn du ihn einschaltest.
- Beim **Herunterladen** von SyncForge (z. B. von GitHub) gelten für den Download die Datenschutzhinweise der jeweiligen Plattform.

## Unterstützung über Ko-fi

Unter **Unterstützen** findest du einen Link zu Ko-fi. Ein Klick darauf öffnet die Ko-fi-Seite in deinem Browser; SyncForge selbst überträgt dabei keine Daten. Was auf der Ko-fi-Seite und bei einer freiwilligen Unterstützung verarbeitet wird, richtet sich nach den Datenschutzbestimmungen von Ko-fi und des gewählten Zahlungsdienstleisters. Der Anbieter erhält dabei nur die Angaben, die Ko-fi an Empfänger von Unterstützungen weitergibt (z. B. den von dir angegebenen Namen und eine Nachricht).

## Deine Rechte

Da der Anbieter keine personenbezogenen Daten von dir erhält, liegen keine Daten vor, über die Auskunft erteilt oder die berichtigt oder gelöscht werden könnten. Alle von SyncForge gespeicherten Daten befinden sich unter deiner Kontrolle auf deinem Gerät.

## Kontakt

Ronny Severin
Fragen zum Datenschutz gern über [GitHub](https://github.com/RNYSVRN/SyncForge/issues).
