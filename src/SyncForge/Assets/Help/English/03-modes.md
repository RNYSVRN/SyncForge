<!-- glyph:E8AB -->
# Sync modes and conflicts

The mode defines how source and destination relate to each other.

## Mirror

The destination becomes an **exact copy of the source**. New and changed files are copied, and files that no longer exist in the source are **deleted** in the destination.

Deleted files go to the **Recycle Bin** on local drives (if enabled). Network drives have no Recycle Bin; files are deleted permanently there.

## Versioned

Like mirror, but nothing is lost: files that would be **overwritten or deleted** in the destination are first moved to the hidden folder `.syncforge_versions` in the destination. You find earlier versions under **Backup profiles** › **…** › **Versions**.

In the profile you set **how many versions per file** to keep and **after how many days** they are removed. Cleanup happens after every run.

## Two-way

Changes on **both sides** are applied, including deletions:

- A file was only created or changed on one side → it is copied to the other side.
- A file was deleted on one side and left unchanged on the other → it is deleted there as well.
- A file was deleted on one side but changed on the other → the **changed version is kept** and copied back.

To do this, SyncForge remembers the common state of both sides after every run. On the **first run** this state doesn't exist yet; both sides are merged and **nothing is deleted**.

## Conflicts (two-way only)

A conflict occurs when a file was **changed on both sides** since the last run. What happens is set in the profile under **On conflicts**:

- **Newer version wins**: the file with the more recent modification date replaces the other (with equal dates, the source wins).
- **Source wins**: the source version replaces the destination version.
- **Keep both**: the source version keeps the name; the destination version is saved as `Name (conflict date time).ext` on both sides.
- **Leave unchanged, just report**: both versions stay as they are. The conflict appears in the history (filter **Conflicts**) and again on every run until you resolve it yourself.

> Tip: With **Notifications › On conflicts** in the settings you learn about conflicts immediately.
