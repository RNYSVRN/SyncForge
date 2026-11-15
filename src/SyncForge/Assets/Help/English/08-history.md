<!-- glyph:E9D2 -->
# History

The history stores every run (manual or automatic) with all details.

## Overview

- **Filter** by profile (including deleted profiles) and status.
- **Key figures for the last 30 days:** number of runs, success rate, data transferred and average duration.
- **Chart "Data transferred per day"**: hover over a bar for date, amount of data and number of runs.
- **Run list**: newest first; **Load more** goes further back.

## Status

- **Successful**: all destinations synced without errors.
- **With warnings**: at least one destination was skipped (not reachable or deletion protection).
- **Failed**: at least one file could not be processed, or no destination was reachable.
- **Cancelled**: the run was stopped.

## Run details

Clicking a run shows the result per destination and a list of all individual actions. Filter by **New**, **Changed**, **Deleted**, **Errors** or **Conflicts** and search the list by path. For errors, the Windows message is shown, e.g. that a file is in use by another program.

The folder icon opens the destination in Explorer; **Delete entry** removes the run from the history (the files themselves are not touched).

## Export as CSV

- **Export…** at the top of the history saves all runs matching the selected profile and status filter as a CSV file (not just the ones already loaded).
- **Export file list…** in the run details saves the individual actions of the run, using the selected destination, filter and search term, without the display limit.

The files open directly in Excel with a double click: the separator follows your regional settings, numbers have no thousands separators and times are written as `YYYY-MM-DD hh:mm:ss`.

## Retention

Older runs are removed automatically after the number of days set in **Settings › Data & retention**. You can also clear the whole history there.
