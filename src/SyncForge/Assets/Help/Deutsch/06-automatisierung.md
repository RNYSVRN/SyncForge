<!-- glyph:E787 -->
# Automatisierung

Im Profil-Editor unter **Automatisierung** legst du fest, wann ein Profil von selbst synchronisiert wird. Mehrere Auslöser lassen sich kombinieren.

## Zeitplan

- **In festen Abständen**: z. B. alle 60 Minuten.
- **Täglich**: zu einer festen Uhrzeit.
- **An bestimmten Wochentagen**: z. B. Montag bis Freitag um 18:00.

**Verpasste Termine nachholen:** War der PC aus oder SyncForge beendet, wird beim nächsten Start **einmal** nachsynchronisiert (nicht für jeden verpassten Termin). Ist die Option aus, wird ein Termin nur ausgeführt, wenn er höchstens 10 Minuten zurückliegt.

Den nächsten geplanten Lauf siehst du in der **Übersicht**.

## Bei Änderungen synchronisieren

SyncForge überwacht den Quellordner und synchronisiert, sobald nach der letzten Änderung die eingestellte **Wartezeit** (Standard 30 Sekunden) vergangen ist. So werden viele Änderungen, etwa beim Speichern eines großen Projekts, zu einem Lauf gebündelt. Ändert sich während eines Laufs etwas, folgt danach automatisch ein weiterer.

## Beim Start von SyncForge

Synchronisiert kurz nach dem Start der App. Zusammen mit **Einstellungen › Mit Windows starten** also bei jeder Anmeldung.

## Wenn ein Ziel-Laufwerk angeschlossen wird

Synchronisiert, sobald ein Laufwerk eingesteckt wird, auf dem ein Ziel des Profils liegt, ideal für USB-Festplatten.

## Wann automatische Läufe warten

Automatische Läufe werden zurückgestellt und später nachgeholt, wenn

- der PC im **Akkubetrieb** läuft (**Einstellungen › Leistung**),
- eine **getaktete Verbindung** besteht und ein Ziel im Netzwerk liegt,
- dasselbe Profil gerade synchronisiert wird.

> Wichtig: Automatische Läufe finden nur statt, solange SyncForge läuft, auch geschlossen im Infobereich.
