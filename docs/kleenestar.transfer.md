![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# Export and import of workspaces

*Settings → General settings → General → Workspace export and import*
(`WWW/Settings/WorkspaceTransfer`, group `SettingGroupGeneralGeneral` beside *Maintenance*)
writes chosen workspaces with everything that belongs to them into a zip file and reads such a
file back, also one written by an older or a newer version. The page has two tabs, *Export* and
*Import* (`WebControl/WorkspaceTransferControl.cs`). Page and endpoint are for the
installation's administrators only: an export holds every object of a workspace, classified
ones included.

## Background jobs

Neither runs in the request. `IWorkspaceTransferManager.StartExport` / `StartImport` queue a
`WorkspaceTransferJob`; one background task runs the jobs one after the other, on behalf of the
account that started them (`SessionManager.BeginIdentity`, so the audit log names it), and
records phase and share. The endpoint `/api/1/workspaces/transfer`:

|Call                                                       |Answer
|-----------------------------------------------------------|-----------------------------------------------
|`POST ?action=export&ids=a,b` (no ids: all)                |the job
|`POST ?action=import&dryrun=&overwrite=&ids=`, body = zip  |the job (the framework forms post no file, so the page sends the archive as the body)
|`GET ?job=<id>`                                            |progress (`state`, `progress`, translated `phase`) and, once finished, the translated report
|`GET ?job=<id>&download=true`                              |the file of a finished export
|`GET`                                                      |the latest jobs (the page picks a running job up again after a reload)

Export files are written to `data/transfer/` and removed with their job after 24 hours
(`WorkspaceTransferManager.Prune`, housekeeping of the `BackgroundWorker`). Jobs live in the
memory of the node that runs them: a restart forgets them, and behind a load balancer the page
must reach the same node.

The page scripts: `kleenestar.core.workspace.transfer.jobs.js` (starts, follows, draws the
progress panel), `…export.js` (workspace cards, filter, download link), `…import.js` (drop zone;
nothing runs until *Check* or *Import* is pressed; confirmation before an import that may
overwrite, naming the workspaces when the same file was checked; report).

## What is exported

The model's `Transfer/WorkspaceTransferCatalog` classifies **every** table (checked by
`UnitTestWorkspaceTransfer.Catalog_ClassifiesEveryTable`; a new entity fails it until it is
classified):

|Role      |Meaning                                                                                   |Examples
|----------|------------------------------------------------------------------------------------------|----------------------------------------------
|`Root`    |the workspaces                                                                            |`Workspace`
|`Content` |belongs to an export when one of its owners does; written in full                         |classes, fields, workflows, forms, templates, views, boards, objects, values, commits, comments, attachments, relations, grants, polls, appointments
|`Shared`  |reference data of the installation; only referenced rows; reused by id or name on import, created otherwise |`Category`, `StatusCategory`, `ObjectRelationType`
|`External`|never exported; only id and name of referenced rows (`references/*.json`) for mapping      |`Identity` (user name), `Group`, `Tenant`
|`Excluded`|not part of a workspace                                                                   |sessions, tokens, notifications, watchers, visits, saved searches, audit log, job queues, path index, insights, branding

Owners and references the database does not declare as foreign keys (`Commit.ObjectId`,
`KanbanBoard.WorkspaceId`, `PermissionAssignment.ScopeId`, `ObjectRelation.TypeKey`,
`SecurityLevel.PermittedGroupIds`, …) are listed in the catalog; declared foreign keys are read
from the EF model. An installation-wide value catalog a field is bound to is exported with the
field, but it is not the workspace's: an overwrite never deletes it.

## File format (`WorkspaceTransferFormat`, version 1)

- `manifest.json`: `format` (`kleenestar.workspaces`), `formatVersion`, `schema` (last
  migration), the workspaces, and per table its columns (property, column, type, reference).
- `tables/<Entity>.json`: an array of rows, each an object keyed by **property name**.
- `references/<Entity>.json`: id and name of the accounts, groups and tenants referred to.

Values are written as the database stores them after the property's value converter (an icon as
its address, a list as its JSON text), enumerations **by member name**, dates ISO 8601, binary
data Base64. A reference always names the id (`Guid`) of the row; a join table's foreign key to
a database id (`WorkspaceCategory`, `WorkspaceTenant`, `ClassAllowedChild`) is translated to the
id on export and back on import. Database ids (`RawId`) never leave the installation.

## Tolerant import

- unknown table or column: left out (note); a column the file lacks: stays empty (a required
  text becomes `""`, a column with a database default takes it);
- a value that cannot be read (unknown enum member, malformed date): stays empty (note); numbers,
  booleans and enum members are also accepted as text, numbers as enum values;
- a newer `formatVersion` is noted, not refused; a manifest of another `format` is refused;
- rows keep their ids. Unique indexes that would break leave the row out;
- a reference to a row neither the file nor the database holds is cleared when it may be empty,
  and leaves the row out when it is required (`Requires` in the catalog keeps a row only with its
  reference: an `ObjectRelation` without its target would turn into an external relation);
- accounts, groups and tenants are mapped by id, then by user name / name; unmatched ones are
  never created;
- one transaction: everything or nothing. `dryrun=true` runs every check and writes nothing.

## Overwrite

A workspace of the file whose id exists is skipped, unless the import overwrites
(`WorkspaceImportOptions.Overwrite`, the page's switch, on by default and confirmed before the
write). Then the present workspace and everything it owns is selected as an export would select
it (`WorkspaceExporter.Select`), and the file is reconciled with it by id: rows of the same id
take the file's values (a column the file lacks takes what a new row has, except one the
database fills by default), new rows are created, rows the file no longer holds are deleted.
Join tables, which have no id, are rewritten. Rows outside the workspace that refer to a deleted
row and that the database does not release itself (a relation from another workspace) are
cleared or deleted along (`ReleaseDependents`); cascades take the per-user data of deleted rows
(watchers, visits). Per-user data of rows that stay is kept.

After a write the manager rebuilds the object path index, publishes the relation types and
invalidates the grant cache; every exported and imported workspace is audited
(`AuditAction.Exported` / `Imported`).
