![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Landing Page Concept

The landing page is the shared entry point of a **KleeneStar** installation. It is what every user meets first, and it exists to give orientation: what is waiting for the reader, what the organization currently holds and publishes, what matters enough to be kept in sight, and where to ask when something is unclear.

It is deliberately not a greeting page and not a dashboard. A greeting page says nothing; a dashboard has to be built before it says anything. The landing page is usable on a fresh installation, without any preparation, and is aimed in particular at people who are new and at people who work with the system only occasionally. Users who have outgrown it replace it — the page is composed of fragments, so a dashboard or a personal overview can take its place without any of its parts being rewritten.

## Layout

The page is laid out as a page rather than as a stack of blocks: a full-width head and figure row, then a wide reading column beside a narrow column of things that keep arriving.

```
┌──────────────────────────────────────────────────────────────┐
│ head — date line, greeting, lede, actions                    │
├──────────────────────────────────────────────────────────────┤
│ figures — assigned · watched · shared · my activity          │
├────────────────────────────────────┬─────────────────────────┤
│ news                               │ pinned content          │
│ my work                            │ help and support        │
│                                    │ latest activity         │
│                                    │ help shape KleeneStar   │
└────────────────────────────────────┴─────────────────────────┘
```

The news lead the wide column, shown exactly like the blog overview of a workspace; the reader's own open work follows. The narrow column holds what is looked up rather than read - pinned content, help, activity. Every list on the page except the news is made of one row shape (`LandingRow`: an icon of at most 21px, title, quiet meta line, optional state chip), divided by hairlines rather than framed.

The page was reworked on 2026-09-24 because it read as a collage: news as tiles carrying the class picture at full size, pinned pages as columns of their full descriptions, help split into three narrow columns (a list, an accordion, a step control), four entry-path cards that repeated the sidebar links beside them word for word, and an activity list of *installation started* / *signed in*. The entry paths are gone from the body (the sidebar keeps them), help moved into the side column as one list, and the activity shows work.

| Order | Fragment | Contributes |
|-------|----------|-------------|
| 10 | `LandingHeadFragment` | Date line, greeting, lede, the two actions |
| 20 | `LandingStatsFragment` | The key figures |
| 30 | `LandingMainColumnFragment` | News, my work |
| 40 | `LandingSideColumnFragment` | Pinned content, help, activity, feedback |

The two columns are one fragment each rather than one per section, because a column is what the grid places — a section contributed on its own would be laid out beside the columns rather than inside one. What a section shows stays a separate class (`Landing…Section`), so an add-on adds a section to a column without touching the layout, and the columns stay the only place that decides where something goes.

The grid is attached in `Assets/css/kleenestar.css` through `div:has(> .ks-landing-head)`: the container the fragment manager emits carries no id and no class, so the one child that is always present is what identifies it. Below 1200px it collapses to a single column and the sections simply follow each other. The section control lays its body out as a grid cell, which grows to its widest line; `.wx-section-body` inside the columns is therefore given `min-width: 0`, or a clipped teaser pushes the column past the page.

The page itself contributes nothing — not even the headline, which is hidden on this page because the head carries a kicker above the title and actions beside it, neither of which the headline control expresses.

## Signed out

A visitor who is not signed in sees none of the above. The figures, news and
activity are nothing to show somebody the installation does not know yet, so every landing
fragment - head, figures, both columns and the sidebar links - is gated on `SignedInCondition`,
and `LandingWelcomeFragment` on its complement: the page shows exactly one of the two.

The signed-out page is the installation's greeting beside the sign-in, and nothing else:

- The greeting is `Branding.WelcomeText`, written with the prose editor on the branding page
  (*Settings -> Branding*). It is rendered through `ProseText.ToSafeHtml` - the editor's own
  reader for a document, encoded text for anything else - because the reader is anybody. Without
  one, a built-in sentence names the installation.
- The sign-in is the framework's login control on the session endpoint; a successful sign-in
  reloads the page into the signed-in one.
- The frame's sidebar, headline, breadcrumb and the header's navigation, search and
  notifications are hidden by `kleenestar.css` on this page only (`body:has(.ks-welcome)`);
  they belong to the frame, not to fragments a condition could withhold. "Create" and the
  settings gear are gone for a signed-out visitor everywhere (a fragment condition and the
  settings pages' `AuthenticatedAccessPolicy`). The application title, the help and the avatar
  stay.

Only the account administrators may change the branding record (`/api/1/branding` `PUT`,
`AccountAuthorization`): what a visitor reads before signing in must not be writable by that
visitor.

## Head

The greeting follows the time of day and addresses the reader by their first name. The figures below it are the reader's own, like the greeting. The two actions are the ones that belong to arriving: *Choose start page*, which leads to the dashboards — the page says itself that it can be replaced — and *New issue*, which opens the same creation modal as everywhere else.

## Key figures

The figures describe the reader, not the organization (changed 2026-09-25: the row used to count all issues, people, teams and everybody's activity - numbers nobody could act on). They follow the personal entries of the sidebar, so every figure is a list one click away. Every count goes through `IObjectManager`, so security levels and content visibility apply exactly as on the lists.

| Figure | Counts | Second line |
|--------|--------|-------------|
| Assigned to me | Open issues assigned to the reader - the *My work* definition (`LandingWorkSection.GetOpen`), scanned up to `ScanLimit`; shown as `150+` when more are assigned than the scan reads | How many are in the `in progress` status category |
| Watching | Active objects the reader watches (`LandingScope.GetWatchedIds`) | How many changed this week |
| Shared with me | Active objects shared with the reader (`LandingScope.GetSharedIds`) | How many changed this week |
| My activity today | The reader's work events of today (`LandingActivitySection.WorkQuery`, actor = reader) | How long ago their last one was |

A bare number says little. "8" next to "3 changed this week" says whether something the reader follows needs a look; the second line is what turns the row from a scoreboard into orientation.

The personal part of the page is the first section of the wide column.

## My work

The open issues assigned to the reader, changed last first, each with key, workspace, age and its state as a chip; *All my issues* leads to `/mine`. "Open" is read from the workflow: an issue whose state falls into the `done` status category is left out, one of a class without a workflow stays. The state is a value row, not a column, so it cannot be part of the query: `LandingWorkSection.GetOpen` reads the assigned issues newest first in pages and stops once it has enough, bounded by `ScanLimit`. Security levels apply as everywhere, through `ObjectManager`.

## Latest activity

The audit log is mostly the installation talking to itself, so the list shows **work**: events a `User` caused on an `Object` in the `Content` or `Workflow` category (`LandingActivitySection.WorkQuery` / `IsWork`), each object once (its latest event), and only an object the reader may open - the audit log is not narrowed by security levels, `ObjectManager.GetObject` is, so a classified record does not leak its title through the feed. The row names the object; the meta line says who did what and when.

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

Four paths lead into the work - as **sidebar links** of the start page (`Landing…SidebarLinkFragment`); the page body no longer repeats them as cards. Three are personal, one is the organization's:

| Path | Route | Slice |
|------|-------|-------|
| My issues | `/mine` | Issues assigned to the caller plus the ones they raised |
| Organization issues | `/workspaces` | Every workspace and the issues within |
| Shared issues | `/shared` | Everything shared with the caller |
| Watched issues | `/watched` | Everything the caller is watching |

The shared and watched slices are not restricted to issues: a share or a watch can be placed on any object, and a page that silently dropped a shared document would be the only place hiding it.

The target pages list through one definition (`LandingScope`). A page caps what it renders and says so when the cap bites.

## Seeded content

`KleeneStarDbSeeder.Landing` seeds thirteen help pages (`SD-9000` … `SD-9012`) as documents of the service-desk knowledge class and attaches the label of the group each belongs to — one page per entry rather than one per group, so each group reads as a list rather than as a single row. It also pins a small set of documents. `KleeneStarDbSeeder.Shares` hands out shares and watches so the two personal entry paths are populated as well.

Both passes skip what is already present, so the step runs on every start: a page added to the set later reaches an installation that was seeded before it existed. Descriptions are written as plain prose. An FAQ entry leads to its page like a guide does.

Together they make the landing page show all of its sections on a fresh installation. The point of a page meant to work without preparation is lost if half of it is empty at first sight.

## Presentation

The page is assembled from WebExpress controls, not from markup of its own:

| Part | Control |
|------|---------|
| Key figures | `ControlGroup` holding four `ControlStat` |
| Section and card headings | `ControlSection` (header, icon, note, badge) |
| News | `ControlDataFeed` over `/api/1/blogs/feed` - the blog overview's control |
| My work, pinned content, help, activity | `LandingRow` — `ControlPanel`, `ControlIcon`, `ControlLink`, `ControlText` |
| State of an issue | the class overview's chip (`ks-co-chip`, via `LandingHtml.StateChip`) |
| Help shape KleeneStar | `ControlCallout` |

The news are the blog overview's stream read across workspaces: `WebRestApi/RestApiBlogFeed` holds what a post looks like in a feed (teaser, pictures, labels, likes, comments, read marker) and is the base of both `/api/1/blogs/{workspacekey}/feed` and `/api/1/blogs/feed`; the latter takes the active posts of every workspace the reader may see and names the workspace in the meta line. Three posts, then *show more posts*.

The row icon is bounded to 21px by `.ks-landing-row > .ks-landing-row-icon`; the row has to be in the selector because the framework's `img.wx-icon` (height 1.2em) outranks a single class.

`ControlGroup` was added to `WebExpress.WebUI` for this page and still carries the key figures. Things placed side by side are read as one statement about one subject; left as separate framed boxes they read as separate claims. The group gives them one surface, divides the fields evenly across the available width, and draws the rule between them — including where a row wraps, which only the laid-out geometry can answer. It ships with its own controller, stylesheet, unit tests, headless JavaScript tests, a tutorial page and `docs/js/group.md`.

What lives in `Assets/css/kleenestar.css` is the page grid, the head and the row shape (`.ks-landing-row…`). Everything else, including the behaviour in dark mode, comes from the controls: they resolve their colours from the framework tokens, which is what makes the page follow the theme without a second palette of its own.
