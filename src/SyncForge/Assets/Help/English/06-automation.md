<!-- glyph:E787 -->
# Automation

In the profile editor under **Automation** you define when a profile syncs by itself. Several triggers can be combined.

## Schedule

- **At fixed intervals**: e.g. every 60 minutes.
- **Daily**: at a fixed time.
- **On specific weekdays**: e.g. Monday to Friday at 6:00 pm.

**Catch up on missed runs:** If the PC was off or SyncForge wasn't running, it syncs **once** at the next start (not once per missed run). If the option is off, a scheduled run only happens if it is at most 10 minutes overdue.

The next scheduled run is shown in the **Overview**.

## Sync on changes

SyncForge monitors the source folder and syncs once the configured **wait time** (default 30 seconds) has passed after the last change. This bundles many changes, such as saving a large project, into one run. If something changes during a run, another run follows automatically.

## When SyncForge starts

Syncs shortly after the app starts. Combined with **Settings › Start with Windows** this means at every sign-in.

## When a destination drive is connected

Syncs as soon as a drive holding one of the profile's destinations is plugged in, ideal for USB drives.

## When automatic runs wait

Automatic runs are postponed and caught up later if

- the PC is running **on battery** (**Settings › Performance**),
- a **metered connection** is active and a destination is on the network,
- the same profile is already syncing.

> Important: Automatic runs only happen while SyncForge is running, also when closed to the notification area.

## Command line

For scripts or the Windows Task Scheduler, SyncForge can also run without a window:

- `SyncForge.exe --run "Documents"`: sync a profile (name or ID, can be repeated)
- `SyncForge.exe --run-all`: sync all profiles
- `SyncForge.exe --list`: list profiles with their ID
- `SyncForge.exe --help`: overview of the commands

The run appears in the history as **Command line**. If SyncForge is already open, the open program takes over the sync and shows its progress. The exit code tells scripts the result: 0 success, 1 warning, 2 error, 3 cancelled, 4 profile not found, 5 invalid arguments, 6 already running.

> Tip: In the Task Scheduler, enter the path to `SyncForge.exe` as the program (after installation in `%LOCALAPPDATA%\Programs\SyncForge`) and, for example, `--run "Documents"` as arguments.

## Backing up locked files (shadow copy)

Some programs keep their files locked, for example Outlook its data files (.pst). In the profile, turn on **Read locked files via shadow copy** under **Safety**. If a file stays locked after several attempts, SyncForge creates a snapshot of the drive (volume shadow copy), copies the file from it and deletes the snapshot after the run.

Windows only allows shadow copies with **administrator rights**. To have this work automatically without a prompt, set up a task:

1. Open the **Task Scheduler** and choose **Create Task**.
2. Under **General**: enter a name and tick **Run with highest privileges**.
3. Under **Triggers**: e.g. daily at 10:00 PM.
4. Under **Actions**: program `SyncForge.exe` (after installation in `%LOCALAPPDATA%\Programs\SyncForge`), arguments `--run "Profile name"`.

Without administrator rights the option has no effect: locked files are reported as errors as before and retried on the next run.
