![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Editor Add-ons

The WYSIWYG editor every document, blog entry and description is written in is the framework's (`ControlDataModalEditor`, `TypeEditTextFormat.Wysiwyg`). It offers a registry of **add-ons** - `webexpress.webui.EditorAddOns` - blocks and inline elements an author picks from the add-on library (`{{` or the puzzle button in the toolbar), configures in a property dialog and places in the text. The framework ships generic ones (box, styled line, ...). KleeneStar adds the ones a knowledge base and a service desk ask for, in the category *KleeneStar*.

| Add-on | Type | What it shows |
|--------|------|---------------|
| `ks-object` *Object reference* | inline | An issue, asset or page by its key: icon, key, title as a link, current state. A key the reader cannot open reads as struck through, *not found or not visible to you*. |
| `ks-list` *Object list* | block | A live list of the objects a filter selects - workspace key, class name, object type, open/done, assigned to the reader, an optional WQL condition, a maximum - with state chips and "n more". |
| `ks-count` *Key figure* | block | The number of objects the same filter selects, as a large figure with a caption. |
| `ks-children` *Child pages* | block | The pages directly below the page it stands on, by title. |
| `ks-news` *News* | block | The latest blog posts - of one workspace (key) or of all - exactly as the blog overview and the start page show them: teaser, pictures, labels, likes, comments, read marker, *show more posts*. |
| `ks-toc` *Table of contents* | block | Built from the headings of the document; in the reading view the entries link to anchors it gives the headings. |
| `ks-callout` *Notice* | container | A box the author writes into, as info, tip, warning or danger, with an optional title. |
| `ks-status` *Status label* | inline | A coloured label in the text - *In progress*, *Approved*. |

## How an add-on lives

The document stores an add-on as a node `{ type: "addon", attrs: { name, data, inline, container } }` - its **name and its properties, nothing it displays**. The editor and the reading view (`ControlContent`, client side) both draw it anew through the add-on's `renderer`, and a controller registered for the rendered element (`webexpress.webui.Controller.registerClass`) brings it to life. So a list in a page is as current as the issues it names, every time the page is opened, and a saved document never carries a stale copy of somebody else's data.

Everything lives in `Assets/js/editoraddons.js`, included on every page of the application with the client translations it is labelled with (`IncludeEditorAddonsScript`: `Assets/js/i18n/en.js`, `de.js`, keys `kleenestar.core:editor.addon.*`) - every page, because documents are edited in dialogs and read in panes on pages that are no object page, and an add-on the registry does not know is drawn as nothing. The looks are in `kleenestar.css` under *editor add-ons*.

## Where the data comes from

`/api/1/editor/objects` (`WWW/Api/_1_/Editor/Objects.cs`), whose address every page names in `<meta name="kleenestar.editor.objects">` (`EditorAddonMetaFragment`), because a script cannot know the application's base path:

- `?key=SD-12` - one object;
- `?parent=<key or id>` - the documents below it (`EditorObjectQuery.Children`);
- `?workspace=&class=&kind=&state=open|done&mine=1&wql=&max=` - a filter (`EditorObjectQuery.Run`).

Every read goes through `ObjectManager`, so **the answer is the reader's**: permissions and security levels narrow it, the same list in the same document shows different objects to different readers, and a reference to a classified record is answered as not found rather than described.

Workspace, class, kind and assignee are columns and narrow the query. The **state** (read from the workflow's status category, as the boards do) and a **WQL** condition cannot - the state is a value row, and a WQL operator such as `~` does not translate to SQL - so they are applied to the rows as they are read, newest first, page by page, bounded by `EditorObjectQuery.ScanLimit` (2000). Beyond that the total is a lower bound, reported as `truncated` and shown as `≥`. A WQL condition that does not parse is refused with 400 and the parser's reason, translated; the list shows it in place of its rows, so the author learns why it stays empty. Blank or `any` as kind means every kind (not `ObjectKind.Normalize`, which reads blank as the default kind).

*News* is not drawn by KleeneStar at all: its controller builds the blog overview's own `webexpress.webapp.FeedCtrl` over `/api/1/blogs/feed` (all workspaces) or `/api/1/blogs/{workspacekey}/feed` (address in `<meta name="kleenestar.editor.news">`). A feed normally takes its data service from a `wx-service` island the server writes into its host; the editor's widget sanitizer lets no such element through, so the controller puts the descriptor the island would have carried where the service registry keeps parsed islands (`host._wxServiceDescriptors`) and constructs the feed itself.

*Child pages* needs to know which page it stands on: the detail pages name the object in `<meta name="kleenestar.object">`, the edit routes carry the key in the address; on a page not yet saved it says so.

## Traps the implementation handles

- **The controller registry removes the class it adopted an element by.** A widget styled by its registration class loses its looks the moment it comes to life. Every widget therefore carries two classes: `ks-addon-*` for the stylesheet and `wx-kleenestar-addon-*` for the registry. The same holds for the framework's own hosts: the reading view's `wx-webui-content` is gone once adopted - it marks its host `wx-content` instead.
- **The reading view keeps the author's spaces** (`white-space: pre-wrap`), and that inherits into a widget: a line break between two list items in the markup a controller writes is a blank line on the page. The markup is written without whitespace between elements, and the add-on roots reset `white-space: normal`.
- **The reading view builds the document detached** and inserts it in one piece, and the registry adopts a widget while it is still detached. The table of contents therefore waits until its element is connected before it looks for headings and for the document they belong to.
- **Only the add-on's properties are persisted.** A controller may rewrite the widget's inner markup freely: the editor reads an add-on back from its frame's name and data attributes, never from its body (verified: the saved state of a document with a live list carries only `{ name, data }`).
- **The reading view indents every list of the prose**, so the add-on lists name their host in the selector to take the indent back.
- **A notice's title is a property, not text**, so it is drawn from the `data-title` the reading block carries (`::before`); in the editor the frame's header names the add-on instead.

## Known limits

- The portal (a second application) does not include the add-ons; a portal page showing a description with an add-on draws it as nothing.
- The filter names a workspace by key and a class by name as typed text - the framework's property dialog knows text, number and fixed choices, not a lookup.
- The server-side plain text of a description (`ProseText.ToPlainText`, used by tables and teasers) knows nothing of add-ons: a widget contributes no words, a notice its text.
