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

## Immer ausgenommen

Der Versionsordner `.syncforge_versions` und SyncForges eigene Zwischendateien (`.~sf_….tmp`) gehören nie zum Inhalt. Verknüpfungen auf Ordner (Symlinks, Junctions) werden nicht verfolgt.
