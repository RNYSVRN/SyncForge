<!-- glyph:E81C -->
# Versionen wiederherstellen

Im Modus **Versioniert** bewahrt SyncForge überschriebene und gelöschte Dateien auf. So holst du eine frühere Fassung zurück:

1. Öffne **Backup-Profile** und wähle beim Profil **…** › **Versionen**.
2. Suche die Datei (Suchfeld oben rechts). Dateien, die es aktuell nicht mehr gibt, sind als **aktuell nicht vorhanden** gekennzeichnet.
3. Wähle rechts die gewünschte Fassung (neueste oben) und eine Aktion:

- **In der Quelle wiederherstellen**: kopiert die Fassung an ihren ursprünglichen Ort in der Quelle. Beim nächsten Lauf gelangt sie auch ins Ziel; die dort bisher aktuelle Fassung wird dabei selbst als Version aufbewahrt.
- **Speichern unter…**: speichert die Fassung an einem beliebigen Ort, ohne die Quelle zu verändern.
- **Im Explorer anzeigen**: öffnet den Versionsordner mit markierter Datei.

Die Fassung im Versionsordner bleibt bei allen Aktionen erhalten.

## Wo liegen die Versionen?

Im versteckten Ordner `.syncforge_versions` direkt im Zielordner, mit derselben Ordnerstruktur wie die Quelle. Dateinamen erhalten einen Zeitstempel: `Bericht~20261115-143000.docx`.

Hat ein Profil mehrere Ziele (oder im Zwei-Wege-Modus Versionen auf beiden Seiten), wählst du oben aus, welchen Versionsordner du durchsuchst.
