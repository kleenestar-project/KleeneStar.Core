![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar SLA Management

SLA management in **KleeneStar** introduces an independent, class-scoped administration of service-level-agreement policies. An SLA policy binds response, resolution, and escalation expectations to the subset of tickets matched by a list of scope rules. The goal is a consistent, auditable definition of what the business promises about responsiveness, decoupled from the workflow and form features that drive ticket processing.

The `SlaManager` is responsible for the entire lifecycle of policy definitions within a workspace and a class. It ensures that:
- Policy definitions are consistent, valid, and unambiguous within their class.
- Targets (e.g. first response within 30 minutes), scope rules (e.g. priority = High AND contract = Enterprise), and escalation levels are kept together in one transactional unit.
- A clock-bearing calendar is explicitly referenced (`SlaPolicy.CalendarId`) so the policy clock can be evaluated against the right business hours and holidays.
- Auditability and traceability of all policy operations are guaranteed.

The `SlaManager` is complementary to the `CalendarManager`, the `WorkflowManager`, and the `PriorityManager`. Calendars provide the schedule; workflows and priorities provide the lifecycle and severity context that scope rules match against.

## Lifecycle and States

SLA management follows a default lifecycle with the states draft, active, inactive, and archived.

- **draft:** The policy is being prepared and does NOT apply to any ticket. Useful for piloting before rollout.
- **active:** The policy is fully configured and currently being enforced for every ticket matching its scope.
- **inactive:** The policy is no longer enforced (e.g. retired, replaced by a successor) but is kept for historicization.
- **archived:** The policy is archived for historical reference and no longer modifiable.

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                         KleeneStar SLA Policy State Diagram                          ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║              new  ╔════════╗   publish   ╔════════╗   retire    ┌──────────┐         ║
║                ───► draft  ║─────────────► active ║─────────────► inactive │         ║
║                   ╚═════▲══╝             ╚═══▲════╝             └─┬────────┘         ║
║                         │                    │                    │                  ║
║                         │ rework             │ re-activate        │ archive          ║
║                         │                    │                    │                  ║
║                         └────────────────────┴────────────────────┘                  ║
║                                              │                                       ║
║                                              │                                       ║
║                                         ╔════▼═════╗                                 ║
║                                         ║ archived ║                                 ║
║                                         ╚══════════╝                                 ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

## Data Model and Relationships

A policy is uniquely bound to a class. It aggregates a list of targets (`SlaTarget`), a list of scope rules (`SlaScopeRule` — AND across rule types, OR within one type; see *Which Agreements Apply*), and a list of escalation levels (`SlaEscalationLevel`). It references one optional working-hours `Calendar` from the same class (foreign key, set to `null` if the calendar is deleted) and one optional owner identity.

- Key attributes: id (stable Guid), name (unique per class), description, state, priority bucket, calendar reference, notification channels (flags enum), pause-on statuses (comma-separated), icon.
- Targets: list of `SlaTarget` entries describing one measurable milestone each (response, resolution, update, approval, implementation, fulfillment, or custom).
- Scope: list of `SlaScopeRule` entries (priority, contract, customer, catalog, tag, system, site, category, source, type).
- Escalations: ordered list of `SlaEscalationLevel` entries firing after a configurable elapsed time.

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                         KleeneStar SLA Data Model                                    ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║          ┌───────────┐ 1                * ┌──────────┐ 1            * ┌───────────┐  ║
║          │ Workspace ├────────────────────► Class    ◄────────────────┤ SlaPolicy │  ║
║          └───────────┘                    └────▲─────┘                └─┬───────┬─┘  ║
║                                                │ 0,1                    │ 1     │ 1  ║
║                                                │                        │       │    ║
║                                          ┌─────┴────┐ 1                 │       │    ║
║                                          │ Calendar ◄────────FK─────────┘       │    ║
║                                          └──────────┘                           │    ║
║                                                                                 │    ║
║                          ┌─────────────────┬──────────────────────┬─────────────┘    ║
║                          │ *               │ *                    │ *                ║
║                    ┌─────▼─────┐    ┌──────▼───────┐       ┌──────▼─────────────┐    ║
║                    │ SlaTarget │    │ SlaScopeRule │       │ SlaEscalationLevel │    ║
║                    └───────────┘    └──────────────┘       └────────────────────┘    ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

## Software Architecture

The `SlaManager` mirrors the architecture of the other class-bound managers. It is a WebExpress component registered via reflection from `KleeneStar.Core.WebManager.SlaManager`. The public API is exposed via the `ISlaManager` interface and resolved by callers through `CoreHub.SlaManager`. Persistence is delegated to `ModelHub.Sla` (a partial class that holds the policy query, insert, update, and delete code), which talks to the `KleeneStarDbContext`. Child collections (`Targets`, `Scope`, `Escalations`) are managed transactionally with the parent policy: on update, the existing children are removed and replaced with the supplied set.

A reactive event surface is provided through the `SlaAdded`, `SlaUpdated`, and `SlaRemoved` events. The REST and HTML surface is delivered through the `WebFragment/Sla/` fragments and the `WWW/Api/_1_/Slas/` endpoints. The calendar dropdown is fed by a class-scoped endpoint (`WWW/Api/_1_/Slas/_classid_/Calendar`) so that only calendars belonging to the active class can be selected.

```
╔KleeneStar.Core═══════════════════════════════════════════════════════════════════════╗
║                                                                                      ║
║                              ┌────────────────────┐                                  ║
║                              │ <<Interface>>      │                                  ║
║                              │ IComponentManager  │                                  ║
║                              ├────────────────────┤                                  ║
║                              └────────Δ───────────┘                                  ║
║                                       ¦                                              ║
║                     ┌─────────────────┴─────────────────────┐                        ║
║                     │ <<Interface>>                         │                        ║
║    ┌----------------┤ ISlaManager                           │                        ║
║    ¦                ├───────────────────────────────────────┤                        ║
║    ¦                │ SlaAdded:Event                        │                        ║
║    ¦                │ SlaUpdated:Event                      │                        ║
║    ¦                │ SlaRemoved:Event                      │                        ║
║    ¦              1 ├───────────────────────────────────────┤                        ║
║    ¦                │ GetSla(Guid):SlaPolicy                │                        ║
║    ¦                │ GetSlas(ClassId):                     │                        ║
║    ¦                │   IEnumerable<SlaPolicy>              │                        ║
║    ¦                │ GetSlas(IQuery):                      │                        ║
║    ¦                │   IEnumerable<SlaPolicy>              │                        ║
║    ¦                │ Add(SlaPolicy):ISlaManager            │                        ║
║    ¦                │ Update(SlaPolicy):ISlaManager         │                        ║
║    ¦                │ Remove(Guid):ISlaManager              │                        ║
║    ¦                └────────────────Δ──────────────────────┘                        ║
║    ¦                                 ¦                                               ║
║    ¦ create         ┌────────────────┴──────────────────────┐                        ║
║    └----------------► SlaPolicy                             │                        ║
║                     ├───────────────────────────────────────┤                        ║
║                     │ Id:Guid                               │                        ║
║                     │ Name:String                           │                        ║
║                     │ Description:String                    │                        ║
║                     │ State:SlaPolicyState                  │                        ║
║                     │ Priority:SlaPriority                  │                        ║
║                     │ CalendarId:Guid?      (FK Calendar)   │                        ║
║                     │ Notifications:SlaNotificationChannels │                        ║
║                     │ PauseOn:String                        │                        ║
║                     │ Owner:Identity                        │                        ║
║                     │ Class:Class                           │                        ║
║                     │ Created:DateTime                      │                        ║
║                     │ Updated:DateTime                      │                        ║
║                     │ Targets:IEnumerable<SlaTarget>        │                        ║
║                     │ Scope:IEnumerable<SlaScopeRule>       │                        ║
║                     │ Escalations:                          │                        ║
║                     │   IEnumerable<SlaEscalationLevel>     │                        ║
║                     └───────────────────────────────────────┘                        ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### SlaTarget

A single measurable target on a policy, e.g. "first response within 30 minutes" or "resolution within 5 business days".

|Attribute      |Type             |Notes
|---------------|-----------------|-----
|`Id`           |`Guid`           |Stable identifier
|`Name`         |`String`         |Display name, e.g. "First response"
|`Kind`         |`SlaTargetKind`  |`Response`, `Resolution`, `Update`, `Approval`, `Implementation`, `Fulfillment`, `Custom`
|`TargetValue`  |`int`            |Numeric value, expressed in `Unit`
|`Unit`         |`SlaTargetUnit`  |`Minutes`, `Hours`, `Days`, `BusinessDays`
|`PolicyId`     |`Guid`           |Owning policy (FK, cascade-delete)

### SlaScopeRule

A single filter rule that contributes to the scope of a policy. Rules of **different** types are combined with logical AND, rules of the **same** type with OR - a ticket has one priority, so "priority P4 AND priority P5" could never hold, while "P4 or P5" is what such a policy means. See *Which Agreements Apply* for how and which rule types are evaluated.

|Attribute   |Type                |Notes
|------------|--------------------|-----
|`Id`        |`Guid`              |Stable identifier
|`RuleType`  |`SlaScopeRuleType`  |`Priority`, `Contract`, `Customer`, `Catalog`, `Tag`, `System`, `Site`, `Category`, `Source`, `Type`
|`Value`     |`String`            |Match value, e.g. "High", "Enterprise", "VIP-User"
|`PolicyId`  |`Guid`              |Owning policy (FK, cascade-delete)

### SlaEscalationLevel

A single escalation step. After `AfterValue` time (in `Unit`) elapses without the target being met, the comma-separated `Notify` list is alerted.

|Attribute    |Type             |Notes
|-------------|-----------------|-----
|`Id`         |`Guid`           |Stable identifier
|`Level`      |`int`            |1-based ordinal within the policy (unique per policy)
|`AfterValue` |`int`            |Time to wait before firing
|`Unit`       |`SlaTargetUnit`  |Time unit
|`Notify`     |`String`         |Comma-separated list of notifiees (role or team names)
|`PolicyId`   |`Guid`           |Owning policy (FK, cascade-delete)

### SlaNotificationChannels

Flags enum that combines the dispatch channels for SLA breach notifications.

|Flag    |Description
|--------|----------------------------------
|`None`  |No channels enabled
|`Email` |Send notifications by e-mail
|`InApp` |Show in-app notifications

## UI Concepts and Pages

The SLA management UI integrates into the class detail pages. From the class sidebar, the "SLA" item opens the policy overview for the active class. The page lists every policy of the class along with priority, calendar, state, target count, and last-updated timestamp. Per-row actions open the edit/clone/delete forms in a modal.

### SLA Management in Class Editing

Entry into SLA management occurs directly from the class detail page via the "SLA" item in the class sidebar. The sidebar item is `ClassSidebarSlaLinkFragment` and is registered with every class-bound page.

The item - and the calendar item beside it, whose business hours only an SLA counts in - is offered only where the class's kind is measured against service levels at all. That is a statement of the kind, `IObjectKind.ServiceLevels`, read by `ClassServiceLevelCondition`: the document, blog and asset kinds answer `false` (a page, a post or an inventory item is never overdue), the issue kind and every add-on kind keep the default `true`. The routes themselves stay reachable; only the sidebar stops offering them.

### SLA Management (Page)

This page is the central administrative view for all SLA policies of a selected class. The main area is rendered by the `SlaViewFragment` (toggle group), which hosts the `SlaViewTableFragment`. Quick-filter chips ("Active", "Draft", "Inactive", "Critical") sit in the header along with an advanced-search input backed by the SLA WQL endpoint. New policies are created via the "New policy" button.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Service Desk / Incident / SLA                                                    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌SLA───────────────────┐ ┌SLA Content────────────────────────────────────────────────┐║
║│                      │░│                                                           │║
║│  - All               │░│ My SLA policies                  [Search] [+ New policy]  │║
║│  - Active            │░│ ─ Chips ─ [Active] [Draft] [Inactive] [Critical]          │║
║│  - Draft             │░│                                                           │║
║│  - Inactive          │░│ Name              | Pri.   | Calendar      | State | Tgts │║
║│  - Critical          │░│-------------------|--------|---------------|-------|------│║
║│                      │░│ Incident · P1 ·…  |Critical| 24 / 7        |Active |  3   │║
║│                      │░│ Incident · P2 ·…  |High    | Standard ·…   |Active |  3   │║
║│                      │░│ Incident · P3 ·…  |Low     | Standard ·…   |Active |  2   │║
║│                      │░│ Incident · VIP ·…|Critical| 24 / 7        |Active |  3   │║
║│                      │░│ Batch job · …     |Medium  | Night shift   |Draft  |  2   │║
║│                      │░│ Legacy · …        |Low     | Standard ·…   |Inact. |  2   │║
║│                      │░│                                                           │║
║│                      │░│                                   ‹ Prev  1  2  3  Next › │║
║├──────────────────────┤░│                                                           │║
║│                   << │░│                                                           │║
║└──────────────────────┘ └───────────────────────────────────────────────────────────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### SLA Management - New/Edit (Modal)

The "Add SLA policy" and "Edit SLA policy" modals share a form layout. The basic-properties form covers name, description, state, priority, calendar, and the comma-separated pause-on statuses. The calendar dropdown is class-scoped — only calendars belonging to the active class are offered, and the dropdown is populated lazily by the class-scoped REST endpoint. Targets, scope rules, and escalation levels are managed inline; all settings are applied in a single transaction via the SLA REST CRUD endpoint.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔SlaAddEditModal═════════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Add SLA policy / Edit SLA policy                                     │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌SLA  ║│            Name*: [ Incident · Priority 1 · Enterprise             ] │║─────┐║
║│     ║│      Description: [ 24/7 coverage for business-critical incidents …] │║     │║
║│     ║│            State: [ Active                                        ▼] │║olic.│║
║│     ║│         Priority: [ Critical                                      ▼] │║     │║
║│     ║│         Calendar: [ 24 / 7 · Always on                            ▼] │║-----│║
║│     ║│          PauseOn: [ Waiting for customer, Scheduled maintenance    ] │║Tgts │║
║│     ║│                                                                      │║-----│║
║│     ║│ ┌Targets────────────────────────────────────────────────────────┐    │║  3  │║
║│     ║│ │ Response   30 Minutes                                  [+][x] │    │║  3  │║
║│     ║│ │ Resolution  4 Hours                                    [+][x] │    │║  2  │║
║│     ║│ │ Update      2 Hours                                    [+][x] │    │║  3  │║
║│     ║│ └───────────────────────────────────────────────────────────────┘    │║  2  │║
║│     ║│                                                                      │║  2  │║
║│     ║│ ┌Scope rules (combined with AND)────────────────────────────────┐    │║     │║
║│     ║│ │ Priority = High            [x]                                │    │║t ›  │║
║│     ║│ │ Contract = Enterprise      [x]                                │    │║     │║
║│     ║│ │ System   = Production      [x]                                │    │║     │║
║│     ║│ └───────────────────────────────────────────────────────────────┘    │║     │║
║│     ║│                                                                      │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                       [Save] [Cancel]  ║     │║
║└─────╚════════════════════════════════════════════════════════════════════════╝─────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### SLA Management - Clone (Modal)

Cloning a policy reuses targets, scope rules, and escalations of an existing entry. The new policy starts in `Draft` state with the suggested name `"<original> (Copy)"`. The owner reference and calendar reference are copied as-is so the clone can be activated without further edits if the source policy is already complete.

### SLA Management - Delete (Modal)

Deleting a policy is irreversible. The modal warns that the policy and all its children (`SlaTarget`, `SlaScopeRule`, `SlaEscalationLevel`) will be removed in a single cascade.

### Which Agreements Apply

A ticket is held to the active policies of its class **whose scope covers it** (`WebManager/SlaScope`, 2026-09-23). Before that every active policy ran on every ticket of the class, so an incident showed the clocks of all four priority agreements instead of the one its priority selects. The decision is KleeneStar's, not the framework's: WebExpress times one agreement (`SlaDefinition`, `SlaEvaluator`), and `SlaScope.Select` is called by both surfaces that show or serve a clock - the card and `/api/1/slaclocks/{objectKey}`, which answers `404` for a target of a policy whose scope leaves the ticket out.

|Rule type   |Evaluated against
|------------|-----------------------------------------------------------------------------
|`Priority`  |The value of the class's priority fields - the priority's **name**, which is what the field stores (an id is resolved too). Compared ignoring case. A rule naming no priority of the class can never be satisfied and is **skipped** rather than switching its policy off.
|`Tag`       |The ticket's tags (`ObjectTag.Name`), ignoring case.
|all others  |Not evaluated - `Contract`, `Customer`, `Catalog`, `System`, `Site`, `Category`, `Source` and `Type` name attributes no ticket carries, so they do not restrict. Treating them as "never" would silently retire every policy that states one; the seeded catalogue does.

A policy without rules covers every ticket of its class, and a ticket without a priority is held to no priority-scoped policy.

### SLA Display on the Ticket (Card)

The ticket detail page (`/issue/{objectKey}`) carries an "Service Level Agreement" card rendered by the `IssueSlaCardFragment`. Every active policy of the ticket's class whose scope covers the ticket becomes one agreement group carrying its name, its severity bucket and the summary of how its targets are doing; every `SlaTarget` inside it becomes one `ControlDataSla` tile — a coloured status, a meter of the consumed budget and the time left until the deadline.

The tiles are rendered complete: the clock is derived and evaluated server-side and seeded into the markup, so the card is correct in the first paint and stays readable without JavaScript. The client then counts on its own and re-reads the state from `/api/1/slaclocks/{objectKey}` once a minute, which keeps a tile in step with a colleague who moved the ticket in another tab.

**The clock is derived, not stored.** KleeneStar persists policies, not per-object timers, so `SlaClock` reads the state off the ticket itself:

|Aspect      |Derivation
|------------|-----------------------------------------------------------------------------
|Start       |`Object.Created`.
|Budget      |`SlaTarget.TargetValue` in its `Unit`, as wall-clock time. A `BusinessDays` target counts as 8 hours per day.
|Paused      |The ticket's current workflow status is named in the policy's `PauseOn` list. The stop is dated at `Object.Updated`, because the transition into that status is what stamped the ticket.
|Settled     |The ticket's current workflow status belongs to the `Done` category. The settlement is dated at `Object.Updated`.
|Pause total |Always zero — there is no status history to reconstruct earlier pauses from.

Two consequences follow and are deliberate: pause time accrued *before* the current status is not credited, and an unrelated edit while the ticket is paused moves the stop forward. The working-hours `Calendar` referenced by the policy is not evaluated either, so a policy bound to a business calendar counts nights and holidays against its target. All three resolve themselves the moment a real per-object clock is persisted — `SlaClock` is then the only place that has to change.

For the same reason the tiles carry no pause / resume / settle actions: a manual transition would have to be written somewhere. The way to stop an agreement is to move the ticket into one of the policy's pause statuses.

## Sitemap SLA Management

|Path                                                                       |Page              |Description
|---------------------------------------------------------------------------|------------------|-------------------------------------------------------------
|`/workspaces/{workspaceKey}/classes/{classKey}/slas`                       |SLA mgmt          |Central overview of all SLA policies of a class.
|`/workspaces/{workspaceKey}/classes/{classKey}/slas/add`                   |SLA creation      |Modal for creating a new SLA policy.
|`/workspaces/{workspaceKey}/classes/{classKey}/slas/{slaKey}`              |SLA detail        |Detail view of a single policy with targets, scope, escalations.
|`/workspaces/{workspaceKey}/classes/{classKey}/slas/{slaKey}/edit`         |SLA edit          |Modal for changing scalar properties, targets, scope, escalations.
|`/workspaces/{workspaceKey}/classes/{classKey}/slas/{slaKey}/clone`        |SLA clone         |Modal for duplicating an existing policy.
|`/workspaces/{workspaceKey}/classes/{classKey}/slas/{slaKey}/delete`       |SLA delete        |Modal for confirming permanent removal.

## API Interfaces (REST Endpoints) - SLA Management

For programmatic interaction, automation, and form rendering, SLA policies are exposed via a versioned REST API rooted at `/api/1/slas`. Endpoints use JSON, follow REST conventions, and are protected by the standard **KleeneStar** authentication and authorization stack.

|Endpoint                                              |HTTP Method |Description
|------------------------------------------------------|------------|------------------------------------------------------------
|`/api/1/slas`                                         |GET         |Lists all policies, paginated and filterable.
|`/api/1/slas`                                         |POST        |Creates a new policy. Requires `name`, `classId`, and (optionally) targets, scope, escalations.
|`/api/1/slas/{slaKey}`                                |GET         |Returns detail of a policy including child collections and the resolved calendar.
|`/api/1/slas/{slaKey}`                                |PUT         |Updates the policy in one transaction.
|`/api/1/slas/{slaKey}`                                |DELETE      |Permanently deletes the policy and cascades to its children.
|`/api/1/slas/state`                                   |GET         |REST selection of `SlaPolicyState` values.
|`/api/1/slas/priority`                                |GET         |REST selection of `SlaPriority` values.
|`/api/1/slas/uniquename`                              |GET         |Validates that a candidate name is available within the class.
|`/api/1/slas/wql`                                     |GET         |WQL prompt suggestions for the SLA advanced search.
|`/api/1/slas/{classKey}/calendar`                     |GET         |Class-scoped REST selection of the active `Calendar` entries belonging to the class. Used by the SLA Add/Edit/Clone calendar dropdown.
|`/api/1/slas/{classKey}/table`                        |GET         |Table-row backing for the SLA view (class-scoped).
|`/api/1/slas/{classKey}/quickfilter`                  |GET         |Quick-filter chips ("Active", "Draft", "Inactive", "Critical").
|`/api/1/slaclocks/{objectKey}?slatargetid={targetId}` |GET         |Returns the running clock of one target on one ticket — `status`, `target`, `elapsed`, `remaining`, `period`, `cycle`, `cycles`, `paused`, `settled` — in the shape the `ControlDataSla` tile adopts. The target must belong to an active policy of the ticket's class.

Standard HTTP status codes apply: `200`/`201`/`204` for success, `400` for validation errors (e.g. duplicate name), `401` for unauthenticated, `403` for forbidden, `404` for unknown policy/class.

## SLA Events

The `SlaManager` publishes the following events via the **WebExpress** `EventManager`. Other managers and UI components can subscribe to react to changes:

|Event Name     |Description
|---------------|----------------------------------------------------------------
|`SlaAdded`     |Triggered when a new policy has been added to a class.
|`SlaUpdated`   |Signals changes to scalar properties or any of the child collections.
|`SlaRemoved`   |Indicates permanent deletion of a policy.

Each event payload carries the affected `SlaPolicy` entity (with its child collections), allowing subscribers to invalidate caches, re-evaluate downstream timers, or update the UI without re-querying.

## Conclusion

This document describes the SLA management concept in **KleeneStar** as the single source of truth for the service-level commitments that the business makes around tickets. The reference implementation comprises the `SlaPolicy`, `SlaTarget`, `SlaScopeRule`, and `SlaEscalationLevel` entities, the `SlaManager` component, a versioned REST API, native WebExpress pages and fragments, and a seeded catalogue that demonstrates the full range of states (active, draft, inactive). The policy directly references a working-hours `Calendar` from the same class via the `CalendarId` foreign key, so the SLA clock evaluation can re-use the central calendar definitions without duplicating schedule logic.
