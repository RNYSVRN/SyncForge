<!-- glyph:E895 -->
# Backup profiles

A profile describes a backup job: **one source**, **one or more destinations**, the **mode** plus filters, safety and automation.

## Managing profiles

The list under **Backup profiles** shows source, number of destinations, mode and last run for each profile. Using the buttons on the right you can:

- **Preview**: analyze without changing anything.
- **Sync now**: run immediately.
- **…** › **Edit**, **Versions**, **Duplicate** or **Delete**.

Clicking a profile also opens the editor. Deleting a profile never touches the files in the source or destinations.

## Sources

A source folder is synced including all subfolders and must exist when you save. With **Add source** you back up several folders in one profile, e.g. documents and pictures.

With **multiple sources**, each goes into its own subfolder of the destinations, named after the folder (e.g. `D:\Backup\Documents` and `D:\Backup\Pictures`). The name is set when the source is added and does not change afterwards. If you switch from one to several sources, files already backed up in the destination stay unchanged. Sources must not overlap. If a source is not reachable, the others are still synced.

## Destinations

Destinations can be:

- **Local drives and USB drives**, e.g. `E:\Backup`
- **Network shares (NAS)**, e.g. `\\nas\backup\documents`
- **Cloud folders**, e.g. your OneDrive, Dropbox or Google Drive folder on the PC

Each destination can be enabled or disabled individually. A destination must not be inside the source (or vice versa). SyncForge prevents this when saving.

**Multiple destinations** are synced either **one after another** or **at the same time**. If a destination is not reachable (e.g. USB drive not plugged in), it is skipped; the other destinations are still backed up.

## Network shares with sign-in

If your NAS requires its own user name and password, a **key icon** appears next to a network path (`\\server\share\...`). Use it to enter the credentials. SyncForge tests them right away and stores them securely in the **Windows Credential Manager**, never in profiles or export files. They apply to all profiles using the same share.

Before every run, SyncForge connects with them automatically, also for schedules and from the notification area. If the share is already reachable (for example because Windows is already connected), the stored credentials are not needed at all. You can remove them in the same dialog.

## USB drives are recognized by the drive itself

For local and USB destinations, SyncForge stores the **serial number of the drive** when you save. If the drive gets a different drive letter next time, SyncForge still finds it. If a different drive is mounted under the old letter, it is never written to.

## Detecting changes

- **Size and date** (default): fast; a file counts as changed if size or modification date differ (±2 seconds tolerance for FAT/exFAT drives).
- **Size and content (checksum)**: for files of equal size, compares the content using SHA-256. Also finds changes without a new date, but is much slower.

In addition, **Verify copies** compares every copied file with the source after writing.
