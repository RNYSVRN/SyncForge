<!-- glyph:E71C -->
# Filter

Mit Filtern bestimmst du, welche Dateien und Ordner SyncForge berücksichtigt. **Ausgeschlossene Dateien werden weder kopiert noch im Ziel gelöscht**. Sie bleiben auf beiden Seiten unangetastet.

## Ausschließen

Ein Muster pro Zeile. Groß-/Kleinschreibung spielt keine Rolle. Zeilen, die mit `#` beginnen, sind Kommentare.

- `*.tmp`: ohne Pfadtrenner: gilt für den Datei- oder Ordnernamen auf **jeder Ebene**.
- `node_modules\`: mit abschließendem `\`: gilt **nur für Ordner**; der ganze Ordner wird übersprungen.
- `Projekte\*.log`: mit Pfadtrenner: gilt für den **Pfad ab dem Quellordner**, hier nur direkt in „Projekte“.
- `Projekte\**\bin\`: `**` steht für beliebig viele Ordnerebenen.
- `datei?.txt`: `?` steht für genau ein Zeichen.

Neue Profile schließen standardmäßig `Thumbs.db`, `desktop.ini`, Office-Sperrdateien (`~$*`) und `*.tmp` aus.

## Nur einschließen

Ist diese Liste leer, werden alle Dateien berücksichtigt. Sonst nur Dateien, die mindestens einem Muster entsprechen, z. B. `*.jpg` und `*.png` für eine reine Bildersicherung. Ordner werden dabei immer durchsucht.

## Weitere Filter

- **Versteckte Dateien ausschließen**
- **Systemdateien ausschließen** (standardmäßig an)
- **Mindest- und Höchstgröße** in MB (0 = keine Grenze)

## Nur online verfügbare Dateien

Cloud-Dienste wie OneDrive, Dropbox oder Google Drive speichern viele Dateien nur als Platzhalter auf dem PC; der Inhalt liegt in der Cloud. Würde SyncForge solche Dateien lesen, lädt der Cloud-Dienst sie zuerst vollständig herunter, unter Umständen viele Gigabyte.

- **Überspringen** (Standard): Online-Dateien werden nicht heruntergeladen und nicht synchronisiert. Eine bereits vorhandene Kopie im Ziel bleibt erhalten und wird nicht gelöscht. Vorschau und Ergebnis zeigen, wie viele Dateien übersprungen wurden.
- **Herunterladen und sichern**: Online-Dateien werden heruntergeladen und wie alle anderen gesichert. Danach liegen sie auch lokal auf dem PC.

> Tipp: Soll ein Cloud-Ordner vollständig gesichert werden, markiere ihn im Explorer mit Rechtsklick › **Immer auf diesem Gerät behalten**, oder wähle **Herunterladen und sichern**.

## Immer ausgenommen

Der Versionsordner `.syncforge_versions` und SyncForges eigene Zwischendateien (`.~sf_….tmp`) gehören nie zum Inhalt. Verknüpfungen auf Ordner (Symlinks, Junctions) werden nicht verfolgt.
