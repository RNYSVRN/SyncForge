<!-- glyph:E81C -->
# Restoring versions

In **Versioned** mode, SyncForge keeps overwritten and deleted files. To get an earlier version back:

1. Open **Backup profiles** and choose **…** › **Versions** on the profile.
2. Find the file (search box at the top right). Files that no longer exist are marked **currently not present**.
3. On the right, choose the version you want (newest first) and an action:

- **Restore in source**: copies the version to its original location in the source. The next run also brings it to the destination; the version that was current there is kept as a version itself.
- **Save as…**: saves the version to any location without changing the source.
- **Show in Explorer**: opens the versions folder with the file selected.

The version in the versions folder is kept with every action.

## Where are versions stored?

In the hidden folder `.syncforge_versions` directly inside the destination folder, with the same folder structure as the source. File names get a timestamp: `Report~20261115-143000.docx`.

If a profile has several destinations (or versions on both sides in two-way mode), choose at the top which versions folder to browse.
