<!-- glyph:E768 -->
# Preview and sync

## Preview

The preview analyzes source and destinations **without changing anything**. For each destination you see:

- how many files are **new**, **changed**, **to delete** and **unchanged**, and how much data would be copied,
- in two-way mode, what goes **to the source** and whether there are **conflicts**,
- under **Planned changes**, the individual steps (up to 500 per destination).

**Sync now** starts the run directly from the preview.

## Deletion protection

If a run would delete more than the share of files set in the profile (default: 25 %), SyncForge shows a warning and does **not start automatically**. Review the planned changes – common causes are a wrong source folder or an empty source that isn't plugged in. Set the value to **0** to turn deletion protection off.

In **automatic runs** an affected destination is skipped instead of asking; the reason is shown in the history.

## During the run

For each destination, a progress bar shows steps, amount of data and the current file. **Cancel** stops the run; files already copied are kept, and half-copied files never occur.

## How copying stays safe

- Each file is first written to a temporary file and only replaces the original once it is **complete** (and verified, if enabled).
- Modification date and attributes are preserved.
- Deleting only happens at the very end of a run.
- If a single file can't be copied (e.g. because it is open), the sync continues; the file appears under **Errors**.

## After the run

The summary shows the result per destination. **Show details** opens the entry in the history. Runs without changes are saved too.
