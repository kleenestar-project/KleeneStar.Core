![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Blueprints

Several departments often work on the same kind of record: every service desk files incidents, every workspace references applications, every team tracks changes. Kept apart, their classes drift - one desk adds an *Impact* field, the next calls it *Severity*, a third forgets the *Waiting* state - and an evaluation across workspaces compares things that only look alike. A **blueprint** defines such a data structure once and provides it to every workspace that follows it, while each workspace keeps its own way of working with it.

## The model

|Concept          |Where                        |Meaning
|-----------------|-----------------------------|------------------------------------------------------------
|Blueprint        |`Class.IsBlueprint`          |a class offered as a shared structure; it is an ordinary class otherwise
|Follower         |`Class.BlueprintId`          |a class of any workspace that receives the blueprint's structure
|Managed copy     |`IBlueprintManaged.BlueprintSourceId` on `Field`, `Status`, `Priority`, `Workflow`, `Transition` |the follower's own row for a blueprint element, naming the element it copies
|Blueprint workspace |a workspace holding blueprint classes |what a new workspace can be set up from

**Copies, not references.** A follower holds its own rows. Everything that reads a class - forms, boards, the values of its objects, WQL, reports - therefore works on a follower exactly as on any other class, and a follower whose blueprint is deleted simply keeps what it has (`Class.Blueprint` is `SET NULL`).

**What is central and what is local.**

|Central (the blueprint's)                          |Local (the follower's)
|---------------------------------------------------|---------------------------------------------------------
|fields and their definition (type, options, cardinality, validation, required, workflow and priority references) |views, tabs, boards, dashboards, quick filters, saved searches
|statuses and their categories                      |create / edit / view forms and their layout
|priorities and their order                          |templates, SLAs, calendars, security levels, permissions
|workflows, their states, transitions, guards, validators, post functions |additional local fields, statuses and transitions
|                                                   |a transition's screen form

## Synchronization

`IBlueprintManager.Synchronize(followerId)` brings a follower in step:

1. **Statuses, priorities, workflows, fields** - in this order, because each later step translates references into the follower's copies of the earlier ones (a workflow's graph into the follower's statuses, a workflow field into the follower's workflow, a priority field's defaults into its priorities).
2. **Matching.** A copy is found by the element it names; an element the follower had before it was bound is **adopted by name**, so binding an existing class does not double its fields and its objects keep their values.
3. **Retiring.** A blueprint element that is gone is retired in the follower - a status, priority or workflow archived, a field deprecated - never deleted, because objects may still carry values of it.
4. **The workflow graph** is written in one context and one save (`ModelHub.SynchronizeWorkflowGraph`): participations and transitions the blueprint provides are added or updated, managed ones it dropped removed, local ones left alone.

**When.** A follower is synchronized when it is bound (class created or updated with a blueprint), whenever an element of its blueprint is added, changed or removed (the structure managers' events; the workflow editor tells the manager after it saved a graph, since it writes without the workflow manager), and for every blueprint at start-up. A synchronization converges, so running it again is always safe - the start-up run heals whatever a crash or a failing follower left behind. It runs as the system, unrestricted and without notifications; the structure managers' events still reach the audit log, so every copied change is traceable.

## Rules

- **A managed copy is changed in the blueprint.** The class-structure endpoints (`RestApiCrudClassStructure`: fields, statuses, priorities, workflows) and the workflow editor refuse to update or delete it locally with a readable refusal (`blueprint.managed`) - a local change would be overwritten by the next synchronization and, until then, make the workspaces disagree about a structure they are meant to share.
- **A follower follows a blueprint of a workspace the caller may read** - the structure is copied into the follower, so following would otherwise disclose a structure the caller cannot see.
- **A blueprint follows nothing, and nothing follows itself** - chains would carry a change through classes whose administrators never agreed to pass it on.
- **A clone follows the blueprint its original follows**, but is not a blueprint itself.

## Surfaces

- **Class dialog** (`ClassEditFormFragment`): *Blueprint* (selection over `/api/1/classes/blueprints`: active blueprint classes of readable workspaces, labelled with their workspace) and the switch *Blueprint for other workspaces*.
- **Workspace wizard** (`WorkspaceAddFormFragment`): *Blueprint workspace* (selection over `/api/1/workspaces/blueprints`). After the template has set the workspace up, `ApplyWorkspace` creates a follower of every blueprint class of the chosen workspace, adopting a class of the same name the template created. A template and a blueprint combine: the template gives the workspace its pages and views, the blueprint the structures several workspaces share.

## Open

Forms are local, so a field a blueprint adds reaches a follower's forms only once a local administrator places it. Blueprints of SLAs, calendars and security levels, a "synchronize now" button and a view of the differences between a follower and its blueprint are not part of this first version.
