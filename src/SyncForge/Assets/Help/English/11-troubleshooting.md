<!-- glyph:E897 -->
# Troubleshooting and FAQ

## A destination is skipped as "not reachable"

The drive or network share is not available, e.g. because the USB drive is not plugged in or the NAS is switched off. As soon as the destination is back, it is backed up on the next run. With **When a destination drive is connected** this happens automatically.

## A file could not be copied

Usually it is open in another program (e.g. Outlook data files). Close the program; the file is copied on the next run. The exact message is shown in the history under **Errors**.

## Deletion protection was triggered

Check in the preview which files would be deleted. If everything is fine (e.g. you deliberately cleaned up a lot), start the run with **Sync now** or adjust the threshold in the profile.

## I don't see any notifications

In the Windows settings under System › Notifications, check whether notifications are allowed for SyncForge and whether **Do not disturb** is on. In that case notifications go silently to the notification center (Windows key + N).

## Deleted files are not in the Recycle Bin

Network drives and NAS have no Recycle Bin; files are deleted permanently there. If you want to be able to get deleted files back, use **Versioned** mode.

## Where does SyncForge store its data?

In `%LOCALAPPDATA%\SyncForge` (Settings › **Data folder**):

- `settings.json`: settings
- `profiles.json`: backup profiles
- `history.db`: history
- `state\`: common state of two-way profiles
- `crash.log`: only after unexpected errors

If SyncForge is installed as an app package (MSIX), Windows keeps this data in a separate, protected area of the app.

## Reporting unexpected behavior

After unexpected errors, SyncForge writes details to the file `crash.log` in the data folder. Attach it to your report.
