<!-- glyph:E895 -->
# Backup-Profile

Ein Profil beschreibt einen Sicherungsauftrag: **eine Quelle**, **ein oder mehrere Ziele**, den **Modus** sowie Filter, Sicherheit und Automatisierung.

## Profile verwalten

In der Liste unter **Backup-Profile** siehst du für jedes Profil Quelle, Anzahl der Ziele, Modus und den letzten Lauf. Über die Schaltflächen rechts kannst du:

- **Vorschau** – analysieren, ohne etwas zu verändern.
- **Synchronisieren** – sofort ausführen.
- **…** › **Bearbeiten**, **Versionen**, **Duplizieren** oder **Löschen**.

Ein Klick auf das Profil öffnet ebenfalls den Editor. Beim Löschen eines Profils bleiben alle Dateien in Quelle und Zielen unverändert.

## Quelle

Der Quellordner wird mit allen Unterordnern synchronisiert. Er muss beim Speichern existieren.

## Ziele

Ziele können sein:

- **Lokale Laufwerke und USB-Festplatten**, z. B. `E:\Backup`
- **Netzwerkfreigaben (NAS)**, z. B. `\\nas\backup\dokumente`
- **Cloud-Ordner**, z. B. dein OneDrive-, Dropbox- oder Google-Drive-Ordner auf dem PC

Jedes Ziel lässt sich einzeln aktivieren oder deaktivieren. Ein Ziel darf nicht in der Quelle liegen (und umgekehrt) – SyncForge verhindert das beim Speichern.

**Mehrere Ziele** werden wahlweise **nacheinander** oder **gleichzeitig** synchronisiert. Ist ein Ziel nicht erreichbar (z. B. USB-Festplatte nicht angeschlossen), wird es übersprungen; die übrigen Ziele werden trotzdem gesichert.

## USB-Laufwerke werden am Laufwerk erkannt

Für lokale und USB-Ziele merkt sich SyncForge beim Speichern die **Seriennummer des Laufwerks**. Bekommt die Festplatte beim nächsten Anschließen einen anderen Laufwerksbuchstaben, findet SyncForge sie trotzdem. Steckt unter dem alten Buchstaben ein anderes Laufwerk, wird es nie beschrieben.

## Änderungen erkennen

- **Größe und Datum** (Standard) – schnell; eine Datei gilt als geändert, wenn Größe oder Änderungsdatum abweichen (±2 Sekunden Toleranz für FAT/exFAT-Laufwerke).
- **Größe und Inhalt (Prüfsumme)** – vergleicht bei gleicher Größe den Inhalt per SHA-256. Findet auch Änderungen ohne neues Datum, ist aber deutlich langsamer.

Zusätzlich kann **Kopien prüfen** jede kopierte Datei nach dem Schreiben mit der Quelle vergleichen.
