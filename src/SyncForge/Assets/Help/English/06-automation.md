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
