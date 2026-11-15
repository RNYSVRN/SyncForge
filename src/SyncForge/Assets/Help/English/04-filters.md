<!-- glyph:E71C -->
# Filters

Filters define which files and folders SyncForge takes into account. **Excluded files are neither copied nor deleted in the destination**. They stay untouched on both sides.

## Exclude

One pattern per line. Case doesn't matter. Lines starting with `#` are comments.

- `*.tmp`: without a path separator: applies to the file or folder name at **any level**.
- `node_modules\`: with a trailing `\`: applies **to folders only**; the whole folder is skipped.
- `Projects\*.log`: with a path separator: applies to the **path from the source folder**, here only directly inside "Projects".
- `Projects\**\bin\`: `**` stands for any number of folder levels.
- `file?.txt`: `?` stands for exactly one character.

New profiles exclude `Thumbs.db`, `desktop.ini`, Office lock files (`~$*`) and `*.tmp` by default.

## Include only

If this list is empty, all files are included. Otherwise only files matching at least one pattern, e.g. `*.jpg` and `*.png` for a pictures-only backup. Folders are always searched.

## More filters

- **Exclude hidden files**
- **Exclude system files** (on by default)
- **Minimum and maximum size** in MB (0 = no limit)

## Online-only files

Cloud services such as OneDrive, Dropbox or Google Drive keep many files only as placeholders on the PC; the content is in the cloud. If SyncForge read such files, the cloud service would first download them completely, possibly many gigabytes.

- **Skip** (default): online-only files are neither downloaded nor synced. An existing copy in the destination is kept and not deleted. Preview and result show how many files were skipped.
- **Download and back up**: online-only files are downloaded and backed up like all other files. Afterwards they are also stored locally on the PC.

> Tip: To back up a cloud folder completely, right-click it in Explorer and choose **Always keep on this device**, or choose **Download and back up**.

## Always excluded

The versions folder `.syncforge_versions` and SyncForge's own temporary files (`.~sf_….tmp`) are never part of the content. Links to folders (symlinks, junctions) are not followed.
