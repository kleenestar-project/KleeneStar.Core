![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Planning

Personal planning, time tracking, milestones and traceability. All of it is optional: an object nobody plans carries none of it, and a workspace that does not track time never asks for it.

## Time tracking and estimates

- **Per workspace.** `WorkspacePlanning.TimeTracking`, off by default, switched on the workspace's *Planning* page (`POST /api/1/planning/{workspacekey} {timeTracking}`, workspace administration).
- **Estimates** live beside the object (`ObjectPlanning`: original, remaining, milestone - a separate table, so the object's search index is untouched). A first estimate is also what is left.
- **Worklogs** (`Worklog`: object, person, start, minutes, comment) reduce the remaining estimate by the time spent unless the person says what is left; the original stays what was planned. A worklog lasts a minute to a day and cannot lie in the future. Logging needs the right to change the object.
- **Privacy.** Every reader of the object sees the totals (estimated · logged · remaining); the entries are shown to their author and to the workspace's administrators, and only they change or remove them.
- **Durations** are typed as people say them (`WebPlanning/Durations`): `1h 30m`, `90m`, `1.5h`, `2d` (a working day = 8 h), `1w` (5 days).

## Capacity and load

Everyone sets their own weekly capacity (`IdentityCapacity`, default 40 h) on **My work** (`WWW/MyWork`, avatar menu): the open objects assigned to them across every workspace, what is left of each, the weeks that takes at their capacity, how many are unestimated, and the week's logged time against the capacity. The workspace's *Planning* page shows the same per assignee for the team (workspace administration - it shows what each person logged).

## Milestones

`Milestone` (workspace, unique name, description, due, state open/reached/archived). An object is put on a milestone of **its own** workspace (*Estimate and milestone* on the object). The milestone page lists its objects - done or not, and how far each is traced upstream ("not traced" marks a change no requirement leads to) - with the progress, the remaining estimates and the logged time. Objects the reader may not see are counted, never named. Removing a milestone keeps its objects.

## Traceability

`WebPlanning/Traceability.Trace` follows the relations of an object both ways over every relation type the installation uses: **downstream** from source to target (requirement → task → change → release), **upstream** from target to source. A trace keeps its direction - a requirement does not reach a sibling requirement through a shared release. Objects are read as the caller may see them; a relation into an object the caller may not see is counted and not followed. The object's planning card shows the counts; `WWW/Work/Trace?object=` lists both directions with hops, relation and state.

## Surfaces

- **Object card** (issues/assets: properties; documents/blogs: below the content): figures and bar (amber when spent + left exceeds the original), milestone, *Log work*, *Estimate and milestone*, traceability.
- **Pages** `WWW/Work/Log|Estimate|Trace?object=`, `WWW/MyWork`, `WWW/Planning/{workspacekey}` (+ `Milestone`, `MilestoneDelete`; area `planning` = workspace administration, cog → *Planning*).
- **REST** `/api/1/work/worklogs` (`GET ?object=`, `POST {object, started, duration, comment, remaining}`), `/work/deleteworklog {id}`, `/work/estimate {object, original, remaining, milestone}`, `/work/capacity` (`GET`, `POST {capacity}`), `/planning/{workspacekey}` (`GET` team and milestones, `POST {timeTracking}`), `/planning/{workspacekey}/milestones`, `/planning/{workspacekey}/deletemilestone`.
- **Export:** the insight object export (`kleenestar.insight.md`) carries the figures through `field:` columns where classes model them; worklogs are personal data and are not exported in bulk.
