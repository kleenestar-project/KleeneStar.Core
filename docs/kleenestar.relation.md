![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Relation Management Concept

In **KleeneStar**, a "relation" denotes a semantic connection between two things: between two objects of the installation, or between an object and an address outside it. The need for such connections grows with every workspace — a project references its tasks, a ticket points at the document that explains it, a change is blocked by the incident it would make worse — and a system whose classes are modelled rather than shipped cannot answer that need with a fixed vocabulary. **KleeneStar** therefore follows a *hybrid link model*: a relation is a first-class entity with its own semantics and its own lifecycle, and the relations that may exist are themselves data an administrator defines. There is no enum of relation kinds anywhere in the system. What "blocks" means, which classes it accepts and what it does to a workflow is a row in a table, editable by the people who run the installation, and nothing in the code knows any relation by name.

Two entities carry the model, and the separation between them is the whole point. `ObjectRelationType` is the **definition** — the abstract statement that a relation of this kind may exist. `ObjectRelation` is the **instance** — one concrete connection that was actually established, by somebody, at some time, for a reason they wrote down.

Relations bundle:
- Type definitions including both labels of the relation, the classes accepted as a target, cardinality, workflow effect and activation state.
- Instances including source, target, direction, lifecycle state, a free-text note, the establishing identity and open key-value metadata.
- Two link categories — object relations addressed by business key and external relations addressed by URI — carried by one structure and read as one list.
- Validation rules for existence, self-reference, accepted classes, duplicates and cardinality, evaluated against both ends before an instance is stored.

## Field-Based versus Entity-Based Linking

Two designs were considered, and the distinction is worth recording because it explains every decision that follows.

A **connection field** treats a link as an ordinary attribute of a class: a field holding a list of ids of one fixed target type. It is cheap, fast and needs no additional table. It is also semantically shallow — it says *this object holds these ids* and nothing about **why** — and it cannot record direction, history, or the fact that two teams mean different things by "related".

A **relation entity** treats a link as an item in its own right, with a type, a direction, metadata and a life of its own. It costs a table, an endpoint family and a surface, and in exchange it can be typed, versioned, validated and visualised.

|                  | Connection field                              | Relation entity
|------------------|-----------------------------------------------|-------------------------------------------
| Shape            | a field on a class holding a list of ids      | a row describing source, target and meaning
| Target types     | exactly one, fixed at definition time         | any, constrained per relation
| Semantics        | none — the field name is the only hint        | typed, named from both ends
| Direction        | implicit, unreadable from the other side      | explicit, readable from both
| Metadata         | none                                          | note, author, timestamp, lifecycle, free key-values
| History          | folded into the object's own commit chain     | the relation has a history of its own
| Visualisable     | no                                            | yes — the graph falls out of the model
| Cost             | one column                                    | one table, one endpoint family, one surface

**KleeneStar** stores the entity. Connection **fields** keep their place in the model, but as navigational conveniences — a shortcut a form offers, a reference a view resolves cheaply. They never carry meaning the relation entity would otherwise hold, and nothing reads a field where it should be reading a relation. That is the hybrid: the simplicity of fields where only navigation is at stake, the expressiveness of entities wherever meaning is.

## Lifecycle and States

A relation follows a lifecycle of three states — **active**, **confirmed** and **obsolete** — and the design decision behind it is that a relation which stopped holding is *not* deleted. The fact that an incident once was blocked by a change is part of the history of both objects, and an incident that *was* blocked is a different story from one that never was.

On creation a relation is **active** and is rendered normally. A relation that a person reviewed and vouched for moves to **confirmed**, which is what distinguishes a curated connection from one an import or a heuristic proposed. A relation whose statement no longer holds moves to **obsolete**: it stays visible in the list, muted and struck through, it no longer occupies its cardinality slot, and *reactivate* is the way back.

Deletion remains available and is deliberately not a state. It exists for a relation that should never have existed — a mistyped target, the wrong type picked in a hurry — because keeping a mistake is not history either.

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                     KleeneStar Relation Lifecycle State Diagram                      ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║                                confirm    ┌───────────────┐                          ║
║                      new  ╔════════╗ ─────►   confirmed   │                          ║
║                        ───► active ║      └──┬─────────┬──┘                          ║
║                           ╚═══▲══╤═╝ ◄────── │         │                             ║
║                               │  │   revoke  │         │ deprecate                   ║
║                     reactivate│  │ deprecate │         │                             ║
║                               │  │           │         │                             ║
║                               │  │  ┌────────▼──┐      │                             ║
║                               └──┴──┤  obsolete ◄──────┘                             ║
║                                     └─────┬─────┘                                    ║
║                                           │                                          ║
║                       ╔═════════╗         │  remove (a mistake, not a change)        ║
║                       ║ deleted ◄─────────┴──────────────  ◄── from any state        ║
║                       ╚═════════╝                                                    ║
║                                                                                      ║
║   An obsolete relation is still rendered and still readable from both ends.          ║
║   It simply no longer counts against the cardinality of its type.                    ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

A **relation type** has a lifecycle of its own, and a much simpler one: it is **active** and offered, or **inactive** and no longer offered while its existing instances keep rendering. A type carrying instances cannot be dropped at all — it is deactivated instead, which is what keeps the meaning of the stored relations intact.

## Data Model

The relation model adds two entities to the core data model. `ObjectRelationType` holds the definitions and is referenced by nothing — it is a catalog. `ObjectRelation` holds the instances and reaches into `Object` twice, once for each end, and into `Identity` for the author.

Three modelling decisions are worth naming. **`Type` is a string key, not a foreign key**: a relation has to keep meaning something after its definition was dropped, and a `RESTRICT` would make the definition undroppable while a `CASCADE` would erase the instances along with it. The endpoint refuses to drop a definition that is still in use, which is the guard that belongs at that level. **`TargetClasses` holds class *names*, not class ids**, because the name is what the wire carries at both ends and what a target reference is validated against; holding ids would mean translating in both directions on every read and would make the rule unreadable in the table. **The four enums are stored by name, not by ordinal**, because they are declared in WebExpress and a member inserted upstream would silently re-read every stored row.

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                        KleeneStar Relation Data Model                                ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║   ┌────────────────────┐                    ┌──────────────────────────┐             ║
║   │ ObjectRelationType │   Type (by Key)    │      ObjectRelation      │             ║
║   ├────────────────────┤◄╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌─┤──────────────────────────┤             ║
║   │ Key       "blocks" │   (no FK — an      │ System                   │             ║
║   │ Label              │    instance must   │ Type      ╌╌► the Key    │             ║
║   │ InverseLabel       │    outlive the     │ Direction Uni | Bi       │             ║
║   │ Symmetric          │    definition it   │ Status    Active |       │             ║
║   │ System             │    was created     │           Confirmed |    │             ║
║   │ TargetClasses  ────┼─╌► class names     │           Obsolete       │             ║
║   │ Cardinality        │    (not a join)    │ Comment                  │             ║
║   │ Effect             │                    │ Metadata  json           │             ║
║   │ Active             │                    │ Created / Updated        │             ║
║   │ Icon / Order       │                    └────┬────────┬────────┬───┘             ║
║   │ Description        │                         │        │        │                 ║
║   └────────────────────┘                  Source │ Target │        │ CreatedBy       ║
║                                           1 (req)│ 0,1    │        │ 0,1             ║
║                                           cascade│ restrict│       │ set null        ║
║                                     ┌────────────▼──┐  ┌──▼─────┐  │ ┌────────────┐  ║
║                                     │    Object     │  │ Object │  └─►  Identity  │  ║
║                                     └───────┬───────┘  └────────┘    └────────────┘  ║
║                                             │ *                                      ║
║                                             │ 1                                      ║
║                                       ┌─────▼─────┐                                  ║
║                                       │   Class   │ ╌╌► Name is the token            ║
║                                       └───────────┘     TargetClasses holds          ║
║                                                                                      ║
║   ┌──────────────────────────────────────────────────────────────────────────────┐   ║
║   │ external relation:  Target = NULL,  TargetUri = "https://…",  TargetTitle    │   ║
║   └──────────────────────────────────────────────────────────────────────────────┘   ║
║                                                                                      ║
║   UNIQUE (Source, Target, Type)   — one relation of one kind between two ends        ║
║   UNIQUE (Key)                    — a relation type is addressed by its key          ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### One Fact, Told From Two Sides

A relation is stored **once**. The end that authored it reads it under the relation's label; the end it points at reads the same row under the inverse label. Which of the two applies is decided by the end the surface sits on, never by a second row.

```
   stored:   FIN-15000  ──── blocks ────►  FIN-15001

   read on FIN-15000:   BLOCKS          FIN-15001  Budget  Marketing Q1     ● In Progress
   read on FIN-15001:   IS BLOCKED BY   FIN-15000  Budget  Hardware Refresh ● Done
```

`Direction` decides whether the second reading happens at all. A **bidirectional** relation appears on both objects; a **unidirectional** one only on its source — which is what an external relation is, since the address it points at knows nothing about it. A **symmetric** type (*similar to*) reads alike from either end and renders no counterpart.

This is also why one type can produce two headings on the same surface: an object that blocks one thing and is blocked by another shows both.

#### Two categories, one structure

|                | Object relation                          | External relation
|----------------|------------------------------------------|--------------------------------
| Link system    | `webexpress.webapp.relation.object`      | `webexpress.webapp.relation.web`
| Target         | another object, by business key          | an absolute `http(s)` address
| Stored in      | `Target` (FK to `Object`)                | `TargetUri` + `TargetTitle`
| Direction      | usually bidirectional                    | always unidirectional
| Validated by   | existence, class rules, cardinality      | the address being absolute

They share one table and one surface, and they are **listed together**, because from the reader's side "what is this connected to" is one question. `Target` is nullable for exactly this reason: it is null precisely when the relation is external.

#### Cardinality

|       | max per source | max per target | example
|-------|----------------|----------------|-------------------------------------------
| `1:1` | 1              | 1              | a document replaces exactly one predecessor
| `1:n` | unlimited      | 1              | a parent aggregates many children, each child has one parent
| `n:1` | 1              | unlimited      | many duplicates point at one original
| `n:n` | unlimited      | unlimited      | a plain reference

The rule is enforced when a relation is created, and against **both** ends: the neighbourhood the check runs over is the relations of the source *and* of the target, so a relation stored from the other side still counts. Two people working in parallel therefore cannot both make the same item a duplicate of a different original.

#### Workflow effect

`Effect` is declared on the type rather than evaluated per instance, so the workflow can ask a single question — *which of my relations block me* — without knowing the semantics of every relation an administrator has invented.

|Effect                |Description
|----------------------|--------------------------------------------------------------
|`None`                |Purely informational; the relation carries no workflow meaning.
|`BlocksCompletion`    |The source cannot reach a closing state while the target is open.
|`ClosesItem`          |Closing the target closes the source — how a duplicate follows its original.
|`AggregatesProgress`  |The progress of the targets is aggregated into the source.

The first two are enforced by `WorkflowManager.ExecuteTransition`, which asks the relation model two questions it does not otherwise need to understand — *may this object close*, and *what closes with it*. The rules themselves live in `ObjectRelationWorkflowRules`, so the workflow never learns the semantics of any relation an administrator invented.

**`BlocksCompletion` is a guard**, evaluated between the workflow guard and the validator stage. Only a move into a *closing* state can be refused, so a blocked object can still be worked on — it simply cannot be finished. The refusal is reported as its own outcome, `WorkflowTransitionOutcome.Blocked`, carrying the keys of the open blockers, so the toast names what has to happen first rather than saying only that something must. An **obsolete** relation refuses nothing: it is kept for the history, and history must not govern what may happen next.

**`ClosesItem` is a post function**, run after the change completed. Every object that declares itself closed with the one that just closed follows it — how a duplicate is settled by its original. A follower is moved along a transition *its own* workflow declares, never by writing a state its state machine forbids; a workflow offering no reachable closing state simply keeps its object where it is. A follower that cannot be moved is not an error of the transition that triggered it, and the cascade is bounded by a visited set, because two objects can be each other's duplicate.

**`AggregatesProgress` is a read-side projection**, not a transition rule: it says how a parent *reports* the state of its children, and there is nothing about it for a transition to refuse or perform. What it computes is described under [Progress aggregation](#progress-aggregation).

> **Which end an effect constrains.** WebExpress ships two descriptions that do not agree. The XML comment on `RelationEffect.BlocksCompletion` says the *source* cannot close while the target is open; the description of its own shipped `blocks` relation — the sentence an administrator reads when picking the effect — says *"the target cannot be completed while this item is open"*. The type descriptions are self-consistent across all three effects and match the labels (`blocks` / `is blocked by`), so they are what is implemented: **the source blocks, the target is blocked**. Implementing the enum comment instead would make the shipped catalog mean the opposite of what its own labels say.
>
> An object whose class models no workflow at all is treated as closed and blocks nothing — otherwise relating a document to a task would make the task unfinishable forever.

## Software Architecture

The application follows a modular, decoupled architectural principle. Two managers divide the subsystem along the line the data model draws: the `ObjectRelationTypeManager` owns the catalog, the `ObjectRelationManager` owns the instances. Neither knows anything about the meaning of a particular relation.

The `ObjectRelationTypeManager` carries the one piece of machinery specific to this subsystem. WebExpress keeps an in-memory `RelationRegistry` that every link surface, the add dialog and the validation read from, and it registers eight relations of its own on first touch. `Publish()` **empties** the registry's relations and lays the stored catalog over it, rather than merging into it. Merging would resurrect a relation the administrator deleted on the next restart, which is precisely the failure a dynamic catalog exists to avoid; the registered link *systems* are left alone, because a system is where a relation may point rather than a relation itself. `Publish()` runs once from `KleeneStarApplication.Run()` and again after every write, so a relation defined in the class administration is offered by every object surface in the next request — with no restart and no deployment.

The `ObjectRelationManager` owns the instances and is a thin, event-raising façade over `ModelHub`. Its `Update` writes back only the changeable fields — type, direction, status, note, external caption, metadata — and never the two ends, because a relation between other objects is a different relation and moving an end would rewrite history rather than correct it.

For a loosely coupled, reactive architecture both managers emit events (see *Relation Events*), which is also how the subsystem reaches the audit log: `AuditManager.Connect()` subscribes centrally, so neither manager contains a single audit call.

```
╔KleeneStar.Core═══════════════════════════════════════════════════════════════════════╗
║                                                                                      ║
║                              ┌────────────────────┐                                  ║
║                              │ <<Interface>>      │                                  ║
║                              │ IComponentManager  │                                  ║
║                              ├────────────────────┤                                  ║
║                              └─────────Δ──────────┘                                  ║
║                                        ¦                                             ║
║                    ┌───────────────────┴────────────────────┐                        ║
║                    ¦                                        ¦                        ║
║   ┌────────────────┴───────────────────┐  ┌─────────────────┴──────────────────┐     ║
║   │ <<Interface>>                      │  │ <<Interface>>                      │     ║
║   │ IObjectRelationTypeManager         │  │ IObjectRelationManager             │     ║
║   ├────────────────────────────────────┤  ├────────────────────────────────────┤     ║
║   │ RelationTypeAdded:Event            │  │ RelationAdded:Event                │     ║
║   │ RelationTypeUpdated:Event          │  │ RelationUpdated:Event              │     ║
║   │ RelationTypeRemoved:Event          │  │ RelationRemoved:Event              │     ║
║   ├────────────────────────────────────┤  ├────────────────────────────────────┤     ║
║   │ Publish():void                     │  │ GetRelations(Guid):                │     ║
║   │ GetRelationTypes():                │  │   IEnumerable<ObjectRelation>      │     ║
║   │   IEnumerable<ObjectRelationType>  │  │ GetRelation(Guid):ObjectRelation   │     ║
║   │ GetRelationType(string):           │  │ GetUsage(string):int               │     ║
║   │   ObjectRelationType               │  │ Add(ObjectRelation):               │     ║
║   │ Store(ObjectRelationType):         │  │   IObjectRelationManager           │     ║
║   │   ObjectRelationType               │  │ Update(ObjectRelation):            │     ║
║   │ Remove(string):bool                │  │   IObjectRelationManager           │     ║
║   └───────────────┬────────────────────┘  │ Remove(ObjectRelation):            │     ║
║                   │                       │   IObjectRelationManager           │     ║
║                   │ Publish()             └─────────────────┬──────────────────┘     ║
║                   ▼                                         │                        ║
║   ┌───────────────────────────────────┐                     │                        ║
║   │ RelationRegistry (WebExpress)     │◄── validates ───────┤                        ║
║   │  in memory, rebuilt on each start │    against          │                        ║
║   └───────────────────────────────────┘                     │                        ║
║                   ▲                                         │                        ║
║                   │ reads                                   │                        ║
║   ┌───────────────┴───────────────────┐   ┌─────────────────▼──────────────────┐     ║
║   │ ObjectRelationType     (table)    │   │ ObjectRelation          (table)    │     ║
║   └───────────────────────────────────┘   └────────────────────────────────────┘     ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

The REST endpoints derive from the WebExpress base classes and implement only the storage questions; the filtering, the grouping, the perspective and the validation are the framework's. `ObjectRelationProjection` translates between the two shapes — the entity holds object ids because that is what a foreign key can be, the framework holds business keys because that is what a person reads — so the translation always passes through the object, which is also where a reference's class, title, route and workflow state come from.

```
  ┌─────────────────────────────┐        ┌──────────────────────────────┐
  │ ControlDataRelationEditor   │        │  ControlDataRelationView     │
  │ class administration        │        │  object view · preview pane  │
  └──────────────┬──────────────┘        └───────┬──────────┬───────────┘
                 │ data                     data │  systems │  targets
                 ▼                               ▼          ▼
   /api/1/relationtypes/{classId}    /api/1/relations/{objectKey}
   RestApiRelationType               RestApiRelation · RelationSystem · RelationTarget
                 │                               │
                 ▼                               ▼
     ObjectRelationTypeManager           ObjectRelationManager
                 └──────────► ObjectRelationProjection ◄──────────┘
```

## UI Concepts and Pages

The following UI mockups show how the relation model is translated into a comprehensible interface. The user interface consistently follows the established design patterns of the **KleeneStar** web application, so the relation surfaces read like the field, form and status surfaces beside them.

Two controls carry the whole subsystem. `ControlDataRelationEditor` is the administrative half and lives in the class administration; `ControlDataRelationView` is the reading half and lives on the object detail pages and in the preview pane. Both are bootstrapped from a host element and build themselves from their REST endpoints, so a relation an administrator defines appears in every surface without a page being touched.

### Relation Management in Class Editing (Sidebar)

The class sidebar carries a "Relations" entry beside Fields, Forms, Statuses, Workflows, Priorities, SLAs and Calendars — a relation is configuration of the same kind, defined once and then offered wherever an object of the class is read. The entry is present on every class-scoped page, and its trailing badge counts the relations the class may actually hold: those that accept it, plus those that name no classes at all.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Finance / classes / Budget / relations                                           │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Class─────────────────┐ ┌Class Content──────────────────────────────────────────────┐║
║│[Budget]              │░│                                                           │║
║│                      │░│  Budget - Relations                                       │║
║│      [Icon]          │░│                                                           │║
║│                      │░│  ( the relation type table, see below )                   │║
║│ ▸ Overview           │░│                                                           │║
║│ ▸ Fields          12 │░│                                                           │║
║│ ▸ Forms            3 │░│                                                           │║
║│ ▸ Statuses         5 │░│                                                           │║
║│ ▸ Workflows        1 │░│                                                           │║
║│ ▸ Priorities       4 │░│                                                           │║
║│ ▸ SLAs             2 │░│                                                           │║
║│ ▸ Calendars        1 │░│                                                           │║
║│ ▸ Relations        8 │◄── the entry this concept adds                              │║
║│                      │░│                                                           │║
║├──────────────────────┤░│                                                           │║
║│ [+] | [Setting]   << │░│                                                           │║
║└──────────────────────┘ └───────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Relation Management (Page)

`/relations/{classId}` is the central management surface for the relations objects of a class may hold. The table names its columns for what they hold rather than for how they are built: **Relation** (the definition — both labels and the direction), **Target type** (the classes accepted, optional), **Cardinality**, **Effect** (the functional meaning, optional), **Usage** (how many instances exist) and **Active**.

The relations themselves are **installation-wide** rather than owned by a class — a relation that only ever joined one class would say very little. The `{classId}` segment names the class the surface is administered *from*: it narrows the table to the relations that accept the class, offers that class's workspace as the checkbox list of possible targets, and is the class the editor writes its preview sentence with.

The drag handle rearranges the relations, and the whole resulting order travels in one request rather than a single moved id, because a drag changes the position of every row below it. The order is a property of the type, so the object surfaces group by it as well.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Class Content──────────────────────────────────────────────────────────────────────┐ ║
║│                                                                                   │ ║
║│  RELATION TYPES OF CLASS Budget     8 active · 8 defined          [ + New type ]  │ ║
║│ ┌──┬ RELATION ──────────┬ TARGET TYPE ─────┬ CARDINALITY ┬ EFFECT ────┬ USAGE ┬ A┐│ ║
║│ │⣿ │ → blocks           │ all types        │ n:n         │ Blocks     │   3   │ ▣││ ║
║│ │  │ ← is blocked by    │                  │             │ completion │       │  ││ ║
║│ │⣿ │ → causes           │ all types        │ 1:n         │ —          │   3   │ ▣││ ║
║│ │  │ ← is caused by     │                  │             │            │       │  ││ ║
║│ │⣿ │ → similar to       │ all types        │ n:n         │ —          │   3   │ ▣││ ║
║│ │  │ ← similar to  ⟲    │                  │             │            │       │  ││ ║
║│ │⣿ │ → funds            │ Invoice Contract │ 1:n         │ —          │   0   │ ▣││ ║
║│ │  │ ← is funded by     │                  │             │            │       │  ││ ║
║│ │⣿ │ → web link         │ all types        │ n:n         │ —          │   3   │ ▣││ ║
║│ │  │ ← —                │                  │             │            │       │  ││ ║
║│ └──┴────────────────────┴──────────────────┴─────────────┴────────────┴───────┴──┘│ ║
║│   ⣿ drag to reorder    ▣ active / ▢ inactive    ⟲ symmetric                       │ ║
║└───────────────────────────────────────────────────────────────────────────────────┘ ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

Removal is guarded. A relation that still carries instances cannot be dropped — it is deactivated instead, which keeps the meaning of the stored relations intact — and the endpoint refuses the request with `relation.type.in.use` even when it is issued directly.

### Relation Type Editor (Modal)

Clicking a row or `+ New type` opens the framework sidebar modal. The editor asks for the one thing a relation really is — a fact told from two sides — so both labels sit next to each other, and the **preview** at the bottom reads the relation back from either end using a real key of the administered class. Below the preview the page reports how many stored instances the change affects, because narrowing the accepted classes or the cardinality of a relation that is already in use is a different decision from defining a fresh one.

Ticking **symmetric** disables the counterpart field and mirrors the label into it; ticking **all classes** clears the individual class picks, because the two statements cannot both hold.

```
╔Modal═════════════════════════════════════════════════════════════════════════════════╗
║ Define relation                                                                  [x] ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║  Reads from the source            Reads from the target                              ║
║  [ funds                     ]    [ is funded by                ]   [ ] symmetric    ║
║                                                                                      ║
║  Description                                                                         ║
║  [ the source pays for the target                                              ]     ║
║                                                                                      ║
║  Accepted target classes                          Cardinality                        ║
║  [ ] all classes                                  ( ) 1:1   (•) 1:n                  ║
║  [x] Invoice   [x] Contract   [ ] Budget          ( ) n:1   ( ) n:n                  ║
║  [ ] CostCenter [ ] Forecast  [ ] Approval                                           ║
║                                                   Effect on the workflow             ║
║  Icon        Active                               [ none                    ▼ ]      ║
║  [ bolt ▼ ]  [x]                                                                     ║
║                                                                                      ║
║  ┌Preview────────────────────────────────────────────────────────────────────────┐   ║
║  │  FIN-15000                 funds           Invoice · e.g. FIN-16000           │   ║
║  │  Invoice · e.g. FIN-16000  is funded by    FIN-15000                          │   ║
║  └───────────────────────────────────────────────────────────────────────────────┘   ║
║  This relation is currently used by 0 stored relations.                              ║
║                                                                                      ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                          [ Cancel ]      [ Save ]    ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Relations in the Object View (Page)

The relation surface sits in the content column of every object detail page, grouped by what the relation says, with a **List** and a **Graph** reading of the same data. Every row starts with the icon of its relation, so a relation stays recognisable by what it says even when the group heading has scrolled out of sight. Picking a row opens the detail dialog of that relation; clicking the key itself follows it, because that is what a relation is for.

The graph is derived from the relations already loaded rather than from a second endpoint, so switching the presentation costs no round trip and the two readings can never disagree. Both readings show **one hop**, which is what the surface is: the relations *this* object holds. What a change to it reaches further out is a different question, and it has a page of its own — see *Impact analysis* below.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Object Content─────────────────────────────────────────────────────────────────────┐ ║
║│                                                                                   │ ║
║│ 🔗 RELATIONS  3                         [ ≣ List | ⁘ Graph ]      [ + Relation ]  │ ║
║│ ──────────────────────────────────────────────────────────────────────────────────│ ║
║│ ⚑ BLOCKS               (counterpart: is blocked by)                             1 │ ║
║│ ⚑ FIN-15001   Budget   Marketing Q1                  ● In Progress  since 28.08 › │ ║
║│                                                                                   │ ║
║│ ↗ WEB LINK                                                                      2 │ ║
║│ ↗ https://github.com/kleenestar-project   KleeneStar on GitHub      since 28.08 › │ ║
║│ ↗ https://example.com/policy              Spending policy           since 28.08 › │ ║
║│                                                                                   │ ║
║└───────────────────────────────────────────────────────────────────────────────────┘ ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

The **graph** renders the same relations laid out by connection instead of by relation. Every node is a rectangle carrying what its row in the list carries — the icon of its relation, the key, the class and title of the object, and its state as a coloured dot. The object the surface belongs to is painted with the primary accent, so the reader sees at once whose relations they are looking at.

```
                      ┌───────────────────────────┐
                      │ ⚑ FIN-15001    ● In Prog. │
                      │   Budget · Marketing Q1   │
                      └─────────────▲─────────────┘
                                    │ blocks
                      ╔═════════════╧═════════════╗
                      ║ 🔗 FIN-15000       ● Done ║   ← the rendering object
                      ║    Budget · Hardware Refr.║
                      ╚═════════════╤═════════════╝
                                    │ web link
                      ┌─────────────▼─────────────┐
                      │ ↗ github.com              │
                      │   KleeneStar on GitHub    │
                      └───────────────────────────┘
```

### Relations in the Preview Pane

The preview pane — the detail side a list row opens — shows the same relations from the same endpoint, but **read-only**, and renders nothing at all when the object holds none. A pane a few hundred pixels wide is where somebody checks what an object is connected to while working through a list; establishing a relation means picking a target, a type and a note, which is work for the full view, and the pane's "open" button is what gets there. Suppressing the add affordance there also keeps the modal dialog out of a frame it would overflow. The graph stays available, because it is the reading a narrow column actually benefits from.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Objects (List)───────────────────┐┌Preview──────────────────────────────────────────┐║
║│ FIN-15000  Hardware Refresh   ◄ ││ Hardware Refresh                    [ Open ›  ] │║
║│ FIN-15001  Marketing Q1         ││ ─────────────────────────────────────────────── │║
║│ FIN-15002  Cloud Spend          ││ ▸ Details                                       │║
║│ FIN-15003  Travel Budget        ││ ▸ Description                                   │║
║│ FIN-15004  Training Budget      ││                                                 │║
║│                                 ││ 🔗 RELATIONS  3       [ ≣ List | ⁘ Graph ]      │║
║│                                 ││ ⚑ BLOCKS                                      1 │║
║│                                 ││ ⚑ FIN-15001  Budget  ● In Progress    28.08 ›   │║
║│                                 ││ ↗ WEB LINK                                    2 │║
║│                                 ││ ↗ github.com   KleeneStar on GitHub  28.08 ›   │║
║│                                 ││                     ( no add affordance )       │║
║│                                 ││ ▸ Comments                                      │║
║└─────────────────────────────────┘└─────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Add Relation (Modal)

`+ Relation` opens the framework sidebar dialog. Its sidebar lists the registered link systems, and the fields of each are a page of that dialog — which is what makes the dialog server-driven: a system contributed by a plugin appears here without any client-side change, rendered by the generic page of its category.

The object page searches the target in a combo box that opens on focus, walks with the arrow keys and reads back as `key - title` once a target is picked. Changing the relation re-queries rather than filtering what is already shown, because which classes are accepted depends on the relation.

```
╔Modal═════════════════════════════════════════════════════════════════════════════════╗
║ Add relation                                                                     [x] ║
╠═══════════════════╦══════════════════════════════════════════════════════════════════╣
║                   ║                                                                  ║
║  ┌─────┐          ║  Link an object of this installation to another one.              ║
║  │ OBJ │ Object   ║                                                                  ║
║  └─────┘          ║  Relation                                                        ║
║  ┌─────┐          ║  [ blocks                                                  ▼ ]   ║
║  │ WEB │ Website  ║                                                                  ║
║  └─────┘          ║  Target                                                          ║
║                   ║  [ FIN-160                                                   ]   ║
║  ── contributed ──║  ┌────────────────────────────────────────────────────────────┐  ║
║  ┌─────┐          ║  │ FIN-16000  Invoice   Dell hardware invoice        ● Open   │  ║
║  │ GH  │ GitHub   ║  │ FIN-16001  Invoice   Cloud subscription Q3        ● Done   │  ║
║  └─────┘          ║  └────────────────────────────────────────────────────────────┘  ║
║                   ║                                                                  ║
║                   ║  Note                                                            ║
║                   ║  [ same gateway - the change has to land first             ]     ║
║                   ║                                                                  ║
║                   ║  FIN-15000 blocks the object you pick.                           ║
╠═══════════════════╩══════════════════════════════════════════════════════════════════╣
║                                                          [ Cancel ]      [ Add ]     ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

`validate` returning a message keeps the dialog open, so an incomplete draft — no relation picked, no target chosen, an address that is not `http(s)` — never reaches the server. The framework submit is synchronous and closes the dialog, so a rejection only the server can see (a duplicate that appeared meanwhile, an exhausted cardinality) arrives after the dialog is gone and is reported as a popup notification carrying the server's code.

## Impact Analysis

The relation surface answers *what is this connected to*. The question a change raises is a different one — *what does changing this touch* — and it is not answered one hop away: an incident blocks a change, the change blocks a release, and the person closing the incident sees only the first of those three. **Impact analysis** is that question, answered by walking the relation graph transitively.

It is not a walk of *every* relation. The model already says which relations have consequences and in which direction they run — that is what `Effect` is for, and the workflow guard has always read it. The analysis asks the same question of the whole neighbourhood instead of of one object: **the guard answers "may I close this", the analysis answers "what happens if I do"**. Reading them out of one place is deliberate; an analysis that predicted a consequence the guard then refuses to have would be worse than none.

|Effect                |The consequence runs |Read as
|----------------------|---------------------|--------------------------------------------------
|`BlocksCompletion`    |source → target      |while the blocker is open, what it blocks cannot be finished
|`ClosesItem`          |target → source      |closing the original settles the duplicate
|`AggregatesProgress`  |target → source      |moving a child moves what the parent reports
|`None`                |nowhere              |informational: two records have something to do with each other, not one governing the other

Note that the direction is **not** the direction the relation was stored in. A duplicate is stored from the follower and carries its consequence backwards along that arrow; the analysis draws the edge the way the consequence runs, which is why an edge is labelled with what the relation *does* (`blocks`, `closes`, `rolls up into`) rather than with the label the relation surface shows.

The walk is bounded four ways, because a relation graph has no natural end: by the **depth** the reader asks for, by a **visited set** (two objects can be each other's duplicate — a mistake somebody made, not a case to refuse), by a **node budget** the result reports as `Truncated` (a truncated analysis that looked complete would be read as "nothing else is affected"), and by **clearance**: an object the caller may not see is not in the answer, and neither is anything reachable only through it — a chain learned through a record one may not open is that record, spelled differently. An obsolete relation carries nothing, and an external address ends a path rather than continuing it: it has no state a change could touch.

`IObjectImpactManager.Analyze(objectId, depth)` is the whole of it; the walk is breadth-first, so an object appears once, at the shortest distance it was reached on, and the distance is part of the answer.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║ GDPR Audit 2025                                          [ 1 | 2 |(3)| 5 | 10 ]      ║
║┌──────────────────────────────────────────────────────────────────────────────────┐  ║
║│                                                                                  │  ║
║│      ┌ CMDB-1002 ─── Done ┐                                                      │  ║
║│      │ SOC2 Type II       │                                                      │  ║
║│      └────────────────────┘                                                      │  ║
║│                 ▲                                                                │  ║
║│               blocks                                                             │  ║
║│                 │                                                                │  ║
║│      ┌ CMDB-1001 ── In Progress ┐        ┏ CMDB-1000 ── To Do ┓                  │  ║
║│      │ ISO 27001 Check          │ ◄─────┫ GDPR Audit 2025      ┃  ← the origin    │  ║
║│      └──────────────────────────┘blocks  ┗━━━━━━━━━━━━━━━━━━━━┛     in accent    │  ║
║│                                                                                  │  ║
║└──────────────────────────────────────────────────────────────────────────────────┘  ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

Every node carries the three things a reader needs to judge a consequence without opening the record: the **key** names it, the **summary** says what it is, the **state** says whether the connection still matters. The origin is painted in the accent, the way the relation graph paints its subject.

**The depth lives in the address** (`?depth=`), not in a stored preference, so a reading can be handed on — "three steps out from this incident" — and the page needs no state of its own. The chooser offers 1, 2, 3, 5 and 10; the last is the manager's own ceiling, so it cannot promise a walk the analysis would clamp.

**It is a page rather than a third tab of the relation surface.** The list and the graph there show the relations of one object; a consequence three steps out belongs to none of them, and putting the two in one switcher would let a reader take the second for a longer version of the first. The way in is a button on the relation page.

## Progress Aggregation

`AggregatesProgress` says that one object reports the progress of others. Until it was computed, a parent showed the progress of its *own* state — which for a container is nearly meaningless, because a container is never itself *in progress*, only the things under it are.

`IObjectProgressManager.GetProgress(objectId)` answers the question, and the rule is one sentence: **an object that aggregates nothing reports the progress of its own workflow state; an object that aggregates reports the average of what it aggregates.** The first half is the reading the plan views have always used (`ObjectBoardProjection.CategoryProgress`: to do 0, in progress and waiting 50, done 100), and it stays that reading on purpose — a parent bar and a child bar in the same chart may not measure differently.

|What is counted            |Answer
|---------------------------|--------------------------------------------------------------
|Which objects              |the **targets** of the relations this object is the *source* of, whose type declares the effect — "the progress of the targets is aggregated into the source"
|Which relations            |active and confirmed ones; an **obsolete** relation counts nothing, so removing a child stops moving the parent's bar
|How each child counts      |at **its own** rolled-up percentage, so a tree is summarised from the bottom up
|How they are weighted      |**equally** — see below
|A child the caller may not see |not at all, and its state does not leak through an average

**Every child counts once and equally.** Weighting by estimate, story points or remaining effort is the obvious alternative and was rejected: it needs a number the model does not require any class to carry, and a rollup that silently ignored the classes without it would report a different figure for the same tree depending on how it was modelled. An unweighted average is the honest reading of what is actually known — *this many of these are done* — which is also why the surface states the count beside the percentage.

**Nothing is stored.** The percentage is a statement about the children as they are now; a copy on the parent would be wrong from the next transition onwards without anything saying so, and the parent's own commit chain would fill with changes nobody made. The recursion is bounded by a visited set (two objects may aggregate each other — a mistake somebody made, not a case to refuse) and by a depth, past which a child counts with its own state.

**The rollup overrides the parent's own state, deliberately.** A parent closed while two of its children are still open reports what the children say, not 100 %. The status card beside it still shows *Closed*, so the reader sees both — and the disagreement between them is information, not a glitch.

Two surfaces read it: the **progress card** in the property column of an object that aggregates (a bar, the percentage, and *3 of 5 finished*), which renders on no other object, and the **plan**, where a parent bar reports its subtree instead of its own state.

```
╔ Property column ═════════════════════╗
║ ▤ STATUS                             ║
║   Status  [ ✓ Closed          ▾ ]    ║
║                                      ║
║   Fortschritt                        ║
║   ┌────────────────────────────────┐ ║
║   │▓▓▓▓▓▓▓▓ 25 %                   │ ║
║   └────────────────────────────────┘ ║
║   0 von 2 abgeschlossen              ║
╚══════════════════════════════════════╝
```

## Sitemap

The sitemap defines navigation paths and visibility logic within the application. The relation surfaces add one page of their own; the reading surfaces are contributed to pages that already exist, so they carry no route.

|Path                        |Page                     |Description
|----------------------------|-------------------------|------------------------------------------------------------
|`/relations/{classId}`      |Relation Management      |Administration of the relations objects of the class may hold.
|`/issue/{objectKey}`        |Object Detail View       |Hosts the relation surface of the object (contributed fragment).
|`/document/{objectKey}`     |Document Detail View     |Hosts the relation surface of the object (contributed fragment).
|`/blog/{objectKey}`         |Blog Detail View         |Hosts the relation surface of the object (contributed fragment).
|`/asset/{objectKey}`        |Asset Detail View        |Hosts the relation surface of the object (contributed fragment).
|`/issue/{objectKey}/relations`|Relation Page          |The relation surface as a page, and the way to the impact analysis.
|`/issue/{objectKey}/impact` |Impact Analysis          |What changing the object touches, as a graph. Reads `?depth=`.
|`/issue/{objectKey}/preview`|Object Preview           |Hosts the read-only relation surface of the object.

## API Interfaces (REST Endpoints)

For programmatic interaction, third-party integration, and automation purposes, **KleeneStar** provides a standardized REST API for relations and their definitions. The interface adheres to REST principles and uses JSON as the data exchange format. Standard HTTP status codes indicate the outcome of each request.

The instances are managed via the following endpoints:

|Endpoint                                  |HTTP Method |Description
|------------------------------------------|------------|--------------------------------------------------------------------
|`/api/1/relations/{objectKey}`            |GET         |Relations of the object, grouped by relation and read from its end. Supports `kind`, `type`, `system`, `status`, `target` and `q`.
|`/api/1/relations/{objectKey}`            |POST        |Establishes a relation from the object.
|`/api/1/relations/{objectKey}/{id}`       |PUT         |Changes the type, direction, lifecycle state or note of a relation.
|`/api/1/relations/{objectKey}/{id}`       |DELETE      |Removes a relation.
|`/api/1/relations/systems`                |GET         |Registered link systems and the relations each offers. Supports `kind` and `enabled`.
|`/api/1/relations/targets`                |GET         |Candidates for the target of a relation. Supports `q`, `type`, `system`, `source` and `l`.
|`/api/1/impact/{objectKey}`               |GET         |The impact analysis of the object as a graph of nodes and edges. Supports `depth`.

The definitions are managed via the following endpoints:

|Endpoint                                  |HTTP Method |Description
|------------------------------------------|------------|--------------------------------------------------------------------
|`/api/1/relationtypes/{classId}`          |GET         |The catalog, narrowed to the relations accepting the class. Supports `q`, `class` and `system`.
|`/api/1/relationtypes/{classId}`          |POST        |Defines a relation.
|`/api/1/relationtypes/{classId}/order`    |POST        |Rearranges the relations; the whole resulting order travels.
|`/api/1/relationtypes/{classId}/{id}`     |PUT         |Changes a relation.
|`/api/1/relationtypes/{classId}/{id}`     |DELETE      |Drops a relation that carries no instances.

A refused write is answered as **400** with `{ "code", "message" }`, and the code is an i18n key, so the surface reports **what** the server objected to rather than a bare status:

|Code                          |Meaning
|------------------------------|-----------------------------------------------------------
|`relation.duplicate`          |The same relation between the same two ends already exists.
|`relation.self`               |An object cannot be related to itself.
|`relation.target.class`       |The relation does not accept the class of the chosen target.
|`relation.cardinality`        |The cardinality of the relation is exhausted at one of the ends.
|`relation.invalid.address`    |An external relation carries no absolute `http(s)` address.
|`relation.unknown.source`     |The source object does not exist.
|`relation.unknown.target`     |The target object does not exist.
|`relation.unknown.type`       |The relation is not registered.
|`relation.inactive.type`      |The relation is deactivated and may no longer be used.
|`relation.type.in.use`        |The relation type still carries instances and can only be deactivated.
|`relation.type.duplicate`     |A relation type with that key already exists.

A request the caller is not permitted to make is answered as **403 Forbidden** before any of the above is evaluated; see *Permissions Model*.

Successful operations return:
- **200 OK** - for successful GET, POST and PUT requests
- **204 No Content** - for successful DELETE and reorder operations

## Relation Events

Relation management follows an event-driven architecture so state changes are communicated transparently across the system. Other components subscribe without being directly dependent on either manager, which is also how the audit log records the subsystem without a single audit call living inside it.

The following events are emitted:

|Event Name             |Emitted by                    |Description
|-----------------------|------------------------------|--------------------------------------------------
|`RelationAdded`        |`ObjectRelationManager`       |A relation has been established between two ends.
|`RelationUpdated`      |`ObjectRelationManager`       |The type, direction, lifecycle state or note of a relation changed.
|`RelationRemoved`      |`ObjectRelationManager`       |A relation has been removed.
|`RelationTypeAdded`    |`ObjectRelationTypeManager`   |A new relation has been defined.
|`RelationTypeUpdated`  |`ObjectRelationTypeManager`   |An existing relation definition changed.
|`RelationTypeRemoved`  |`ObjectRelationTypeManager`   |A relation definition has been dropped.

Each event carries the affected entity, from which the audit bridge derives the target, the attribute-level deltas and the acting identity. Instances are recorded under the `Content` category against `AuditTargetType.Relation`; definitions are recorded under `Configuration` against `AuditTargetType.RelationType`, because changing what a relation means changes what every object of every class may state.

## Permissions Model

Relation management is governed by the object-level permission model rather than by one of its own: a relation is a statement *about* objects, and the right to make it follows the right to edit them.

|Permission          |Description
|--------------------|----------------------------------------------------------------------------------
|`object_relation`   |Allows establishing, changing and removing the relations of an object.

The permission is attached to the two policies that already govern object editing:

|Policy                |Description                                       |Included Permissions
|----------------------|--------------------------------------------------|----------------------------------
|`object_edit_policy`  |Allows editing objects and their relations.       |`object_relation`, …
|`object_admin_policy` |Full administrative control over objects.         |`object_relation`, …

Administering the **definitions** is a class-level concern and is governed by the class permission model (`class_update` for changing the catalog of a class's relations, `class_admin_policy` for full control).

### How a check is evaluated

`IPermissionManager.IsGranted` answers whether an identity holds a permission on a resource, and the relation endpoints call it before every read and every change. The evaluation walks a **chain** rather than a single resource, because a grant does not have to sit on the record being touched to govern it:

|Endpoint                          |Read requires        |Write requires           |Chain
|----------------------------------|---------------------|-------------------------|---------------------------------
|`/api/1/relations/{objectKey}`    |`object_read`        |`object_relation`        |object → class → workspace
|`/api/1/relationtypes/{classId}`  |`class_read`         |`class_update`           |class → workspace

The three questions asked in turn are: which groups the identity belongs to, which policies those groups were granted anywhere on the chain, and whether any of those policies carries the permission. The last question is put to the framework's component registry rather than answered by reading attributes locally, so a policy the application declares and one a plugin contributes are judged by the same rule. A grant naming a policy the running system no longer knows carries nothing — it was written against a component that is gone, and reading it as a grant of everything would turn an uninstalled plugin into an escalation.

> **An unadministered resource is not a forbidden one.** When no grant exists anywhere on the chain, the answer is *allow*: the installation has never expressed a restriction, and reading "nobody said yes" as "everybody is refused" would make every record unreachable the moment a guard is put in front of it. As soon as a single grant exists on the chain, the chain is administered and the permission is enforced — from that point an identity in no granted group, and a caller that cannot be resolved at all, are both refused.

A refused request is answered as **403 Forbidden**. An unresolvable *route* is not an authorization decision: a key naming no object is answered as not found, because refusing there would turn a typo into a permission error.

`/api/1/relations/systems` and `/api/1/relations/targets` are not gated. The first answers the relation catalog, which is configuration rather than content; the second searches objects and is bounded by the same visibility the object list already offers. Both are named here rather than left to be discovered.

> **Note.** The relation endpoints are the first under `/api/1` to evaluate permissions. The evaluation itself lives on `PermissionManager` and takes a resource chain, so gating a further endpoint is one call — but until an installation administers a grant, every one of them behaves exactly as before.

## Conclusion

The document "KleeneStar Relation Management" outlines the conceptual framework for connecting objects within and beyond the installation. It settles the field-versus-entity question in favour of a hybrid in which the entity carries all meaning and fields remain navigational conveniences, and it establishes that the vocabulary of relations is data rather than code: an administrator defines what may exist, and the application interprets that structure without knowing any relation by name.

The model is enforced rather than merely described: a relation that declares `BlocksCompletion` refuses the move it says it refuses, a relation that declares `ClosesItem` settles what follows it, the impact analysis walks them transitively to say what a change reaches, a relation that declares `AggregatesProgress` is what a parent's progress is computed from, and the endpoints evaluate `object_relation` and `class_update` against the resource chain before they read or write anything.

As a high-level specification, the document still leaves one aspect open, named explicitly rather than implied: the **customer portal** does not yet project the relations of an issue. Contributed **link systems** are a seam the framework already serves end to end, but **KleeneStar** itself contributes none beyond the two native ones.

The focus lies on structural clarity, semantic depth and contributor empowerment. The proposed model supports typed and directed connections, lifecycle rather than deletion, validation against both ends, and graph visualisation, while remaining open to the extensions above.
