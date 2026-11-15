<!-- glyph:E713 -->
# Settings

## Appearance

- **App theme**: Light, Dark or **Use Windows setting** (follows the Windows setting automatically).
- **Language**: German, English or the Windows setting. The language changes after SyncForge restarts (**Restart now**).

## General

- **Start with Windows**: SyncForge starts in the notification area without a window when you sign in.
- **Keep running in the notification area**: closing the window doesn't exit SyncForge; schedules and monitoring keep running. If the option is off, closing exits the app.
- **Notifications**: Windows notifications after runs, configurable separately for **success**, **errors** and **conflicts**. After manual runs, a notification only appears if the window is hidden.

## Performance

- **Parallel copy operations**: how many files are copied at the same time (1–16).
- **Bandwidth limit**: limits the transfer rate of all copies in KB/s (0 = unlimited), useful for network destinations. Profiles with their own limit use theirs.
- **Pause on battery power** and **Pause on metered connections**: both apply to automatic runs.

## Data & retention

- **Keep history (days)** and **Clear history**.
- **Import & export**: saves all profiles and settings to a file or loads them, e.g. on a new PC. On import, profiles with the same ID are replaced and the others added; applying the settings is optional.
- **Data folder**: opens the folder where SyncForge stores settings, profiles and history.

## Updates

- **Check for updates**: When the switch is on, SyncForge asks GitHub once a day whether a new version exists. Only your IP address and the SyncForge version are transmitted. The check is off by default.
- If there is a new version, a notice appears in the **Overview** (and a notification, once) with a link to the download page. Nothing is installed automatically: download the new installer and run it, your profiles and settings are kept. With winget you can also use `winget upgrade RonnySeverin.SyncForge`.
- **Check now** checks right away.
