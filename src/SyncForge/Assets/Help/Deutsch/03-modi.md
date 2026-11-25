<!-- glyph:E8AB -->
# Sync-Modi und Konflikte

Der Modus legt fest, wie Quelle und Ziel zueinander stehen.

## Spiegeln

Das Ziel wird zur **exakten Kopie der Quelle**. Neue und geänderte Dateien werden kopiert, und Dateien, die es in der Quelle nicht mehr gibt, werden im Ziel **gelöscht**.

Gelöschte Dateien landen auf lokalen Laufwerken (wenn eingeschaltet) im **Papierkorb**. Auf Netzlaufwerken ist kein Papierkorb möglich; dort wird endgültig gelöscht.

## Versioniert

Wie Spiegeln, aber nichts geht verloren: Dateien, die im Ziel **überschrieben oder gelöscht** würden, verschiebt SyncForge vorher in den versteckten Ordner `.syncforge_versions` im Ziel. Frühere Fassungen findest du unter **Backup-Profile** › **…** › **Versionen**.

Im Profil legst du fest, **wie viele Fassungen je Datei** behalten werden und **nach wie vielen Tagen** sie entfernt werden. Aufgeräumt wird nach jedem Lauf.

## Zwei-Wege

Änderungen auf **beiden Seiten** werden übernommen, auch Löschungen:

- Eine Datei wurde nur auf einer Seite neu angelegt oder geändert → sie wird auf die andere Seite kopiert.
- Eine Datei wurde auf einer Seite gelöscht und auf der anderen nicht verändert → sie wird auch dort gelöscht.
- Eine Datei wurde auf einer Seite gelöscht, auf der anderen aber geändert → die **geänderte Fassung bleibt** und wird zurückkopiert.

Dafür merkt sich SyncForge nach jedem Lauf den gemeinsamen Stand beider Seiten. Beim **ersten Lauf** gibt es diesen Stand noch nicht; dann werden beide Seiten nur zusammengeführt, und es wird **nichts gelöscht**.

## Konflikte (nur Zwei-Wege)

Ein Konflikt entsteht, wenn eine Datei seit dem letzten Lauf auf **beiden Seiten geändert** wurde. Was dann passiert, wählst du im Profil unter **Bei Konflikten**:

- **Neuere Fassung gewinnt**: die Datei mit dem jüngeren Änderungsdatum ersetzt die andere (bei gleichem Datum gewinnt die Quelle).
- **Quelle gewinnt**: die Fassung aus der Quelle ersetzt die im Ziel.
- **Beide behalten**: die Quellfassung behält den Namen, die Zielfassung wird als `Name (Konflikt Datum Uhrzeit).ext` auf beiden Seiten gespeichert.
- **Nicht ändern, nur melden**: beide Fassungen bleiben, wie sie sind. Der Konflikt erscheint in der Historie (Filter **Konflikte**) und bei jedem Lauf erneut, bis du entscheidest. Über **Konflikte lösen** (in der Vorschau und in den Laufdetails) siehst du beide Fassungen mit Größe und Datum und wählst je Datei: **Quelle behalten**, **Ziel behalten**, **Beide behalten** oder **Später entscheiden**. Mit **Für alle übernehmen** geht das auch in einem Schritt, etwa „jeweils neuere Fassung“.

> Tipp: Mit **Benachrichtigungen › Bei Konflikten** in den Einstellungen erfährst du sofort davon.
