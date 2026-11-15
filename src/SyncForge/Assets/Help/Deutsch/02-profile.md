<!-- glyph:E895 -->
# Backup-Profile

Ein Profil beschreibt einen Sicherungsauftrag: **eine Quelle**, **ein oder mehrere Ziele**, den **Modus** sowie Filter, Sicherheit und Automatisierung.

## Profile verwalten

In der Liste unter **Backup-Profile** siehst du für jedes Profil Quelle, Anzahl der Ziele, Modus und den letzten Lauf. Über die Schaltflächen rechts kannst du:

- **Vorschau**: analysieren, ohne etwas zu verändern.
- **Synchronisieren**: sofort ausführen.
- **…** › **Bearbeiten**, **Versionen**, **Duplizieren** oder **Löschen**.

Ein Klick auf das Profil öffnet ebenfalls den Editor. Beim Löschen eines Profils bleiben alle Dateien in Quelle und Zielen unverändert.

## Quellen

Ein Quellordner wird mit allen Unterordnern synchronisiert und muss beim Speichern existieren. Über **Quelle hinzufügen** sicherst du mehrere Ordner in einem Profil, etwa Dokumente und Bilder.

Bei **mehreren Quellen** landet jede in einem eigenen Unterordner der Ziele, benannt nach dem Ordner (z. B. `D:\Backup\Dokumente` und `D:\Backup\Bilder`). Der Name wird beim Hinzufügen festgelegt und ändert sich danach nicht mehr. Wechselst du von einer auf mehrere Quellen, bleiben bereits gesicherte Dateien im Ziel unverändert liegen. Quellen dürfen sich nicht überschneiden. Ist eine Quelle nicht erreichbar, werden die übrigen trotzdem synchronisiert.

## Ziele

Ziele können sein:

- **Lokale Laufwerke und USB-Festplatten**, z. B. `E:\Backup`
- **Netzwerkfreigaben (NAS)**, z. B. `\\nas\backup\dokumente`
- **Cloud-Ordner**, z. B. dein OneDrive-, Dropbox- oder Google-Drive-Ordner auf dem PC

Jedes Ziel lässt sich einzeln aktivieren oder deaktivieren. Ein Ziel darf nicht in der Quelle liegen (und umgekehrt). SyncForge verhindert das beim Speichern.

**Mehrere Ziele** werden wahlweise **nacheinander** oder **gleichzeitig** synchronisiert. Ist ein Ziel nicht erreichbar (z. B. USB-Festplatte nicht angeschlossen), wird es übersprungen; die übrigen Ziele werden trotzdem gesichert.

## Netzwerkfreigaben mit Anmeldung

Verlangt dein NAS einen eigenen Benutzernamen und ein Kennwort, erscheint neben einem Netzwerkpfad (`\\server\freigabe\...`) ein **Schlüsselsymbol**. Darüber hinterlegst du die Anmeldedaten. SyncForge testet sie sofort und speichert sie sicher in der **Windows-Anmeldeinformationsverwaltung**, nie in den Profilen oder in Export-Dateien. Sie gelten für alle Profile mit derselben Freigabe.

Vor jedem Lauf verbindet sich SyncForge damit automatisch, auch bei Zeitplänen und im Infobereich. Ist die Freigabe schon erreichbar (etwa weil Windows bereits verbunden ist), werden die gespeicherten Daten gar nicht gebraucht. Entfernen kannst du sie im selben Dialog.

## USB-Laufwerke werden am Laufwerk erkannt

Für lokale und USB-Ziele merkt sich SyncForge beim Speichern die **Seriennummer des Laufwerks**. Bekommt die Festplatte beim nächsten Anschließen einen anderen Laufwerksbuchstaben, findet SyncForge sie trotzdem. Steckt unter dem alten Buchstaben ein anderes Laufwerk, wird es nie beschrieben.

## Änderungen erkennen

- **Größe und Datum** (Standard): schnell; eine Datei gilt als geändert, wenn Größe oder Änderungsdatum abweichen (±2 Sekunden Toleranz für FAT/exFAT-Laufwerke).
- **Größe und Inhalt (Prüfsumme)**: vergleicht bei gleicher Größe den Inhalt per SHA-256. Findet auch Änderungen ohne neues Datum, ist aber deutlich langsamer.

Zusätzlich kann **Kopien prüfen** jede kopierte Datei nach dem Schreiben mit der Quelle vergleichen.
