# Framework integration

The framework boundary places generic form, table and structured content behavior in WebExpress. KleeneStar retains object permissions, field mappings, storage ownership, renderer conversions and publication rules. The application requires the WebExpress 2.0.0-alpha packages containing the form and table extension contracts and structured content extensions. DebugLocal can use the corresponding sibling source projects.

The avatar integration uses the framework upload parser and resolver. AvatarStorage configures the existing application image formats and delegates writes to CoreHub.StoreIcon. Endpoint validation rejects malformed uploads before any record changes and preserves the payload for complete form validation. A submitted URL is accepted only when it equals the record's current image, because an unchanged avatar control resubmits its loaded URL. Missing fields preserve the image, empty fields request removal, and valid uploads receive a content-based address. Storage cleanup happens after the new file has been written.

The table integration uses ControlDataTable and the framework inline-edit template. ObjectTableColumnTemplate supplies application field choices, display kinds and the PUT method. Template composition is represented by RestApiTableRow.Children before serialization, and plain descriptions remain text. The previous response JSON shim, inline-save script, column chooser subclass and unused empty-state implementation have been removed.

The layout compatibility adapter reads both existing array snapshots and the versioned framework format. New snapshots are written through RestApiTableLayout, and the framework applies presentation settings to fresh column definitions. Session ownership and saved-search persistence remain application responsibilities. Existing databases do not require a bulk rewrite.

The content integration delegates format recognition and text extraction to EditorState and SpreadsheetWorkbook. WorkbookText retains the application preview length, conversion from prose, size limit and draft semantics. A null draft body still means unchanged, while an explicitly empty workbook remains an intentional edit.

The editor compatibility registrations retain the persisted names ks-toc and ks-status by delegating to the framework definitions. Existing ks-callout documents retain their variant property and tip value through the legacy definition and styling. New content can use the framework toc, status and callout definitions. Object queries, workspace feeds and object references remain application add-ons, and stored documents are not rewritten during startup.

The form integration creates title inputs through TitleInputFactory for each render request. Workspace dropdown entries use the framework visibility predicate. The previous local title placement and conditional dropdown subclasses have been removed. Server-side authorization remains independent of control visibility.
