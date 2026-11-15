# Privacy statement

> DRAFT – Have this reviewed by a legal professional and add contact details before publication (e.g. in the Microsoft Store).

Last updated: November 2026

## In short

SyncForge works **entirely locally on your PC**. The app sends **no data to the provider** or to third parties, contains **no telemetry, no advertising and no tracking**, and requires **no user account**.

## What data SyncForge processes

To sync folders, SyncForge reads and writes the files and folders you define as source and destination in your profiles. File contents are only copied between these locations.

Stored locally:

- **Settings** (`settings.json`) – e.g. theme, language, notifications.
- **Backup profiles** (`profiles.json`) – paths of sources and destinations, filters, schedules, serial number and label of destination drives.
- **History** (`history.db`) – time and result of every run and the names and paths of the processed files.
- **Two-way state** (folder `state`) – paths, sizes and modification dates of the last synced files.
- **Error log** (`crash.log`) – only after unexpected errors; may contain file paths.

This data is stored in the folder `%LOCALAPPDATA%\SyncForge` (when installed as an app package, in the app's protected data area) and never leaves your PC. You can view it at any time via **Settings › Data folder**, clear the history in the settings, and remove all data by uninstalling or deleting the folder.

## Network and cloud

SyncForge only accesses network shares or cloud folders that you set up as destinations yourself. For cloud folders, the privacy terms of the respective cloud provider apply, whose software transfers the files further.

## Windows features

- **Notifications** are shown by Windows and contain profile names and a summary of the run.
- **Startup** is registered with Windows if you turn it on.
- If obtained through the **Microsoft Store**, Microsoft processes purchase and installation data under its own privacy terms; the provider has no influence on this.

## Your rights

Since the provider receives no personal data from you, there is no data about which information could be provided, or that could be corrected or deleted. All data stored by SyncForge is under your control on your device.

## Contact

Ronny Severin
[Add contact address/e-mail before publication]
