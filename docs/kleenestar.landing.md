![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Landing Page Concept

The landing page is the shared entry point of a **KleeneStar** installation. It is what every user meets first, and it exists to give orientation: what the organization currently holds, what matters enough to be kept in sight, which ways lead into the work, and where to ask when something is unclear.

It is deliberately not a greeting page and not a dashboard. A greeting page says nothing; a dashboard has to be built before it says anything. The landing page is usable on a fresh installation, without any preparation, and is aimed in particular at people who are new and at people who work with the system only occasionally. Users who have outgrown it replace it — the page is composed of fragments, so a dashboard or a personal overview can take its place without any of its parts being rewritten.

## Layout

The page is laid out as a page rather than as a stack of blocks: a full-width head and figure row, then a wide reading column beside a narrow column of things that keep arriving.

```
┌──────────────────────────────────────────────────────────────┐
│ head — date line, greeting, lede, actions                    │
├──────────────────────────────────────────────────────────────┤
│ figures — issues · people · teams · activity                 │
├────────────────────────────────────┬─────────────────────────┤
│ news                               │ pinned content          │
│ entry paths                        │ latest activity         │
│ help and support                   │ help shape KleeneStar   │
└────────────────────────────────────┴─────────────────────────┘
```

News leads the wide column and the pinned content sits opposite it in the narrow one. That is a deliberate pairing: news is what changes between two visits and benefits from the width to be read several entries at a time, while the pinned content is looked up rather than read and keeps its place at the top of the page.

| Order | Fragment | Contributes |
|-------|----------|-------------|
| 10 | `LandingHeadFragment` | Date line, greeting, lede, the two actions |
| 20 | `LandingStatsFragment` | The key figures |
| 30 | `LandingMainColumnFragment` | News, entry paths, help |
| 40 | `LandingSideColumnFragment` | Pinned content, activity, feedback |

The two columns are one fragment each rather than one per section, because a column is what the grid places — a section contributed on its own would be laid out beside the columns rather than inside one. What a section shows stays a separate class (`Landing…Section`), so an add-on adds a section to a column without touching the layout, and the columns stay the only place that decides where something goes.

The grid is attached in `Assets/css/kleenestar.css` through `div:has(> .ks-landing-head)`: the container the fragment manager emits carries no id and no class, so the one child that is always present is what identifies it. Below 1200px it collapses to a single column and the sections simply follow each other.

The page itself contributes nothing — not even the headline, which is hidden on this page because the head carries a kicker above the title and actions beside it, neither of which the headline control expresses.

## Head

The greeting follows the time of day and addresses the reader by their first name. On a page everybody shares, that one personal line is what tells a reader the figures below are the organization's and not theirs. The two actions are the ones that belong to arriving: *Choose start page*, which leads to the dashboards — the page says itself that it can be replaced — and *New issue*, which opens the same creation modal as everywhere else.

## Key figures

The figures describe the organization, not the caller: they are what lets somebody place their own work in a context. They are counted, never loaded — each is a single `COUNT` against a filtered set — because the landing page is hit by everybody at the start of every session and must not drag a table across to print a number. The count helpers live on the managers (`IObjectManager.CountObjects` and its siblings) and pass the query straight through to the model.

| Figure | Counts | Second line |
|--------|--------|-------------|
| Issues | Active objects of kind `issue` | How many were raised this week |
| People | Active identities | How many left a trace in the audit log this week |
| Teams | Active groups | The names of the first few |
| Activity | Audit events of today | How long ago the last one was |

A bare number says little. "112" next to "8 new this week" says whether the queue is growing; "4" next to "IT, Dev, HR, Finance" says which teams those are. The second line is what turns the row from a scoreboard into orientation.

"Active this week" is read from the audit log rather than from a session table, because a session says somebody was signed in, not that they did anything.

The personal figures sit one section further down, on the entry-path cards.

## Reserved labels

The pinned area and the help area own no content of their own. What appears in them is decided by a label on an object — an ordinary `ObjectTag`, the same rows the tag card of an object writes:

| Label | Area |
|-------|------|
| `Pinned` | The pinned area: the org chart, central guidelines, the documents nobody should have to search for |
| `Help` | The compact how-to pages |
| `FAQ` | The frequently-asked-questions pages |
| `First Steps` | The pages that walk a newcomer through their first day |

Help pages are therefore pages: objects of the installation, editable by the people who know the answers, in the editor they already use, and versioned, searchable and translatable like everything else. Promoting one to the landing page needs no separate editor and no separate permission — whoever may label an object may decide what the organization sees on its way in.

Labels are matched case-insensitively and are resolved through `LandingLabel`, which also filters by state: an archived page drops off the landing page without anybody having to remember to strip its label first. `LandingHelpMenuFragment` reads the same three help labels into the header's help menu, so the help of an installation is reachable from every page and not only from the one the reader has just navigated away from.

The labels are English and stable. The display text of an area comes from the resource files, never from the label.

## Entry paths

Four paths lead into the work. Three are personal, one is the organization's:

| Path | Route | Slice |
|------|-------|-------|
| My issues | `/mine` | Issues assigned to the caller plus the ones they raised |
| Organization issues | `/workspaces` | Every workspace and the issues within |
| Shared issues | `/shared` | Everything shared with the caller |
| Watched issues | `/watched` | Everything the caller is watching |

The shared and watched slices are not restricted to issues: a share or a watch can be placed on any object, and a page that silently dropped a shared document would be the only place hiding it.

The figure on a card is the size of the slice behind it, counted through the same definition the target page lists (`LandingScope`), so a card and its page cannot disagree about what the slice is. The page caps what it renders and says so when the cap bites — a card promising eight hundred that led to a page showing twenty-five and saying nothing would be worse than no card at all.

## Seeded content

`KleeneStarDbSeeder.Landing` seeds thirteen help pages (`SD-9000` … `SD-9012`) as documents of the service-desk knowledge class and attaches the label of the column each belongs to — one page per entry rather than one per column, so each column reads as a list rather than as a single row. It also pins a small set of documents. `KleeneStarDbSeeder.Shares` hands out shares and watches so the two personal entry paths are populated as well.

Both passes skip what is already present, so the step runs on every start: a page added to the set later reaches an installation that was seeded before it existed. Descriptions are written as plain prose — an FAQ answer opens directly beneath its question, and markup would be shown as written.

Together they make the landing page show all of its sections on a fresh installation. The point of a page meant to work without preparation is lost if half of it is empty at first sight.

## Presentation

The page is assembled from WebExpress controls, not from markup of its own:

| Part | Control |
|------|---------|
| Key figures | `ControlGroup` holding four `ControlStat` |
| Section and card headings | `ControlSection` (header, icon, note, badge) |
| Pinned content, news, entry paths | `ControlGroup` — a grid of fields, not a row of framed cards |
| Latest activity | `ControlTimeline` |
| Frequently asked questions | `ControlAccordion` |
| First steps | `ControlSteps` |
| Help how-tos | `ControlList` |
| Help shape KleeneStar | `ControlCallout` |

`ControlGroup` was added to `WebExpress.WebUI` for this page. Things placed side by side are read as one statement about one subject; left as separate framed boxes they read as separate claims. The group gives them one surface, divides the fields evenly across the available width, and draws the rule between them — including where a row wraps, which only the laid-out geometry can answer. It ships with its own controller, stylesheet, unit tests, headless JavaScript tests, a tutorial page and `docs/js/group.md`.

What remains in `Assets/css/kleenestar.css` is the page grid and the head. Everything else, including the behaviour in dark mode, comes from the controls: they resolve their colours from the framework tokens, which is what makes the page follow the theme without a second palette of its own.
