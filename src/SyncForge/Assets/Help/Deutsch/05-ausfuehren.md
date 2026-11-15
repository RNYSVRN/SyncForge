<!-- glyph:E768 -->
# Vorschau und Synchronisierung

## Vorschau

Die Vorschau analysiert Quelle und Ziele, **ohne etwas zu verändern**. Für jedes Ziel siehst du:

- wie viele Dateien **neu**, **geändert**, **zu löschen** und **unverändert** sind und wie viele Daten kopiert würden,
- im Zwei-Wege-Modus zusätzlich, was **zur Quelle** übertragen wird und ob es **Konflikte** gibt,
- unter **Geplante Änderungen** die einzelnen Schritte (bis zu 500 je Ziel).

Mit **Jetzt synchronisieren** startest du den Lauf direkt aus der Vorschau.

## Löschschutz

Würde ein Lauf mehr als den im Profil eingestellten Anteil der Dateien löschen (Standard: 25 %), zeigt SyncForge eine Warnung und startet **nicht automatisch**. Prüfe dann die geplanten Änderungen. Häufige Ursachen sind ein falsch gewählter Quellordner oder eine leere, nicht eingesteckte Quelle. Mit **0** schaltest du den Löschschutz ab.

Bei **automatischen Läufen** wird ein betroffenes Ziel übersprungen statt nachzufragen; der Grund steht in der Historie.

## Während des Laufs

Für jedes Ziel zeigt ein Fortschrittsbalken Schritte, Datenmenge und die aktuelle Datei. **Abbrechen** stoppt den Lauf; bereits kopierte Dateien bleiben erhalten, halb kopierte Dateien entstehen nie.

## So sicher wird kopiert

- Jede Datei wird zuerst in eine Zwischendatei geschrieben und ersetzt das Original erst, wenn sie **vollständig** (und auf Wunsch geprüft) ist.
- Änderungsdatum und Attribute werden übernommen.
- Gelöscht wird erst ganz am Ende eines Laufs.
- Kann eine einzelne Datei nicht kopiert werden (z. B. weil sie geöffnet ist), läuft die Synchronisierung weiter; die Datei erscheint unter **Fehler**.

## Nach dem Lauf

Die Zusammenfassung zeigt das Ergebnis je Ziel. Über **Details anzeigen** gelangst du zum Eintrag in der Historie. Auch ein Lauf ohne Änderungen wird gespeichert.
