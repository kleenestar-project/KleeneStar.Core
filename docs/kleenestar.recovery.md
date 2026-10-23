![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Backup, Recovery and Archiving

## Backups

A SQLite installation backs itself up (`WebMaintenance/DatabaseBackups`):

|Setting (`Database:` in `kleenestar.core.settings.json`)|Default        |Meaning
|-------------------------------------------------------|---------------|-----------------------------------------------
|`BackupDirectory`                                      |`data/backup`  |where the backups are written
|`BackupIntervalHours`                                  |`24`           |hours between two automatic backups; `0` switches them off
|`BackupKeep`                                           |`14`           |how many are kept; older ones are removed

- **Consistent while running.** A backup is `VACUUM INTO` - a copy of the database as one transaction sees it, taken without stopping the installation; no write is half in it.
- **Checked.** Every backup passes SQLite's `PRAGMA integrity_check` before it counts; one that does not is removed and the failure logged - a backup is never a promise it cannot keep.
- **Never overwritten.** Each backup is a new file `kleenestar-yyyyMMdd-HHmmss.db`.
- **Scheduled** by the automation worker's hourly housekeeping (`RunDue`); a failing backup is logged and does not stop the queue.
- **On demand:** `POST /api/1/maintenance/backups` (installation administrators); `GET` lists them. The endpoint never serves a backup's content - a copy of the whole database is taken from the server's disk, not downloaded through the application.
- Another database (PostgreSQL, SQL Server) is backed up with its own tools; the endpoint says so.

## Restoring

1. Stop the installation.
2. Keep the current database aside (`data/db/kleenestar.db` and its `-wal`/`-shm` files).
3. Copy the chosen backup to `data/db/kleenestar.db`.
4. Start the installation. It applies the migrations a newer version brings; the search index rebuilds from the database where it does not match (`kleenestar.search.md`).

What a running installation recovers on its own after a crash:

|What                         |How
|-----------------------------|--------------------------------------------------------------------------------------------
|automation jobs              |persistent queue; a job held by a dead worker is claimed again when its lease runs out; a resumed job continues after its last finished action (`kleenestar.automation.md`)
|creations from templates     |an interrupted run is recovered at start (`RecoverInterruptedCreations`, `kleenestar.template.md`)
|approvals                    |decisions are single per approver and step, concurrent decisions are re-decided on the new state (`kleenestar.approval.md`)
|catalog refreshes            |a source that cannot be read keeps the current version (`kleenestar.catalog.md`)

The migrations are tested to build the complete model from an empty database (`UnitTestRecovery`), and a backup is tested to hold the data and pass the check.

## Archiving

Archived objects (state `Archived`) leave the working lists and stay readable, searchable and restorable - nothing is deleted. The automation action **`archive [days]`** archives the object, or - with days - only when it is done and has not changed for that long. The retention rule of a service desk is one scheduled rule:

```
Trigger: schedule, every 1440 minutes, class Incident
Actions: archive 90
```

Finished automation jobs are pruned after 30 days; the audit log keeps its own retention (`kleenestar.audit.md`).
