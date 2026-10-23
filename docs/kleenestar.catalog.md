![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Value Catalogs

Locations, cost centers, applications, suppliers: the same list of values serves selection fields in many classes and workspaces, and it changes. A **value catalog** keeps such a list centrally, versioned, and fed from where it is maintained.

## Catalogs

`ValueCatalog` - a workspace's own, or installation-wide (`WorkspaceId = null`, administered by the installation's administrators). The **source** says where the values come from:

|Source    |Values                                                                 |Refreshed
|----------|-----------------------------------------------------------------------|----------------
|`Static`  |typed in, one per line, `value|label`                                  |by publishing
|`Query`   |the summary or key of every object a WQL query selects (within the catalog's workspace), **read as the catalog's creator** - the values are shown to everyone filling in a bound field | every `RefreshMinutes`, or *Refresh now*
|`External`|another system's address answering a JSON list of strings, of `{ value, label }` (or `id`/`name`), `{ "values": [...] }`, or text/CSV with one value per line | every `RefreshMinutes`, or *Refresh now*

A new query or external catalog is read at once. The automation worker refreshes what is due every minute (`IValueCatalogManager.RefreshDue`). A source that cannot be read keeps the current values and records why (`LastError`) - a list never empties because another system was down. `ValueCatalogManager.Transport` replaces the HTTP handler (tests, proxies).

## Versions

Every change is an immutable `ValueCatalogVersion` (values as JSON, number, added/removed counts, why, who, when). A version is written **only for a real change** - a refresh reading the same list writes nothing - and numbered without gaps under a unique index, so two refreshes racing cannot fork the history. *Bring back version n* publishes n's values as a new version; history only grows. Values are trimmed, empty and repeated ones dropped, the order kept; at most 5,000 per catalog.

## Bound fields

`FieldCatalogBinding` binds a selection field (`Selection`, `MultiSelection`, `Choice`, `Radio`, `Tile`) of the catalog's workspace - any workspace for an installation-wide catalog - and **writes the values into the field's options**, so forms, filters and renderers read them as before. A binding follows the catalog, or is **pinned** to a version and keeps it. A value that leaves a catalog leaves the options; objects that hold it keep it. Releasing a field or deleting a catalog leaves the field's options as they are.

## Surfaces

- **Pages** `WWW/Catalogs/{workspacekey}` (cog → *Value catalogs*; area `catalogs` = workspace administration) and `Catalog?catalog=`: values, settings, versions with restore, bound fields with bind (follow or pin) and release.
- **REST** `/api/1/catalogs/{workspacekey}`: `GET` the catalogs the workspace sees (values, versions, bindings, last error); `POST` saves (`name, description, global, source, query, valueAttribute, url, refreshMinutes, state`) or acts (`action=publish|refresh|restore|bind|unbind|delete` with `values`, `version`, `field`).
- **Audit**: catalogs as configuration, every published version as a notice.
