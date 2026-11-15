<!-- glyph:E897 -->
# Fehlerbehebung und häufige Fragen

## Ein Ziel wird als „nicht erreichbar“ übersprungen

Das Laufwerk bzw. die Netzwerkfreigabe ist nicht verfügbar, z. B. weil die USB-Festplatte nicht angeschlossen oder das NAS ausgeschaltet ist. Sobald das Ziel wieder da ist, wird es beim nächsten Lauf gesichert. Mit **Wenn ein Ziel-Laufwerk angeschlossen wird** geschieht das automatisch.

## Eine Datei konnte nicht kopiert werden

Meist ist sie gerade in einem anderen Programm geöffnet (z. B. Outlook-Datendateien). Schließe das Programm; die Datei wird beim nächsten Lauf kopiert. Die genaue Meldung steht in der Historie unter **Fehler**.

## Der Löschschutz hat angeschlagen

Prüfe in der Vorschau, welche Dateien gelöscht würden. Ist alles in Ordnung (z. B. du hast bewusst viel aufgeräumt), starte den Lauf über **Jetzt synchronisieren** oder passe die Schwelle im Profil an.

## Ich sehe keine Benachrichtigungen

Prüfe in den Windows-Einstellungen unter System › Benachrichtigungen, ob Benachrichtigungen für SyncForge erlaubt sind und ob **Bitte nicht stören** aktiv ist. In diesem Fall landen Benachrichtigungen still in der Mitteilungszentrale (Windows-Taste + N).

## Gelöschte Dateien sind nicht im Papierkorb

Auf Netzlaufwerken und NAS gibt es keinen Papierkorb, dort wird endgültig gelöscht. Wenn du gelöschte Dateien zurückholen können möchtest, verwende den Modus **Versioniert**.

## Wo speichert SyncForge seine Daten?

Unter `%LOCALAPPDATA%\SyncForge` (Einstellungen › **Datenordner**):

- `settings.json`: Einstellungen
- `profiles.json`: Backup-Profile
- `history.db`: Historie
- `state\`: gemeinsamer Stand der Zwei-Wege-Profile
- `crash.log`: nur bei unerwarteten Fehlern

Wird SyncForge als App-Paket (MSIX) installiert, legt Windows diese Daten in einem eigenen, geschützten Bereich der App ab.

## Unerwartetes Verhalten melden

Bei unerwarteten Fehlern schreibt SyncForge Details in die Datei `crash.log` im Datenordner. Füge sie einer Fehlermeldung bei.
