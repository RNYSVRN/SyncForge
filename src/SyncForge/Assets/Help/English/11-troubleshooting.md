<!-- glyph:E897 -->
# Troubleshooting and FAQ

## A destination is skipped as "not reachable"

The drive or network share is not available, e.g. because the USB drive is not plugged in or the NAS is switched off. As soon as the destination is back, it is backed up on the next run. With **When a destination drive is connected** this happens automatically.

## Sign-in to the network share failed

The user name or password is wrong (or no longer valid), e.g. after a password change on the NAS. Open the profile and enter the credentials again using the key icon. If SyncForge reports that Windows is **already connected as a different user**: Windows allows only one sign-in per server at a time. Disconnect the mapped network drive in Explorer or use the same credentials there.

## A file could not be copied

Usually it is open in another program (e.g. Outlook data files). SyncForge waits out short locks, for example by a virus scanner, and retries several times. If the file stays locked, close the program; the file is copied on the next run. For files that are always locked (e.g. Outlook), there is the option **Read locked files via shadow copy** (see **Automation**). The exact message is shown in the history under **Errors**.

## Folders could not be read

SyncForge had no access to these folders, e.g. because of missing permissions or because the network connection dropped briefly during the analysis. What such a folder contains is then unknown. That is why nothing is copied into it and, above all, nothing is deleted, not even on the other side. As soon as access works again, the folder is synced normally on the next run.

## The run stopped because a drive was no longer reachable

The USB drive was unplugged during the run or the connection to the NAS dropped. SyncForge then ends the run right away instead of trying every remaining file one by one. Files already copied are complete, there are never half-written files. The rest is caught up on the next run.

## Deletion protection was triggered

Check in the preview which files would be deleted. If everything is fine (e.g. you deliberately cleaned up a lot), start the run with **Sync now** or adjust the threshold in the profile.

## Files from OneDrive are skipped

These are online-only files. By default SyncForge doesn't download them, so a backup never pulls large amounts of data from the cloud unnoticed. In the profile under **Filters › Online-only files** you can choose **Download and back up**, or keep the folder permanently on the device in Explorer.

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
