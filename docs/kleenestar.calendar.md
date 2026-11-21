![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Calendar Management

Calendar management in **KleeneStar** introduces an independent, fine-grained administration of working-hours calendars per class. A calendar describes when the clock runs for time-sensitive features such as SLA policies, scheduled jobs, or escalations. The goal is a single, consistent representation of business hours, holidays, and time zones that can be referenced from every clock-bound feature in the system.

The `CalendarManager` is responsible for the entire lifecycle of calendar definitions within a workspace and a class. It ensures that:
- Calendar definitions are consistent, valid, and unambiguous within their class.
- The weekly schedule (Mon-Sun) and the holiday list are kept together in one transactional unit.
- Time zone and region are explicitly modelled so cross-region tickets are evaluated against the correct local hours.
- Auditability and traceability of all calendar operations are guaranteed.

The `CalendarManager` is complementary to the `SlaManager` (and any future time-sensitive feature). Calendars provide the schedule. SLA policies and similar features reference them by id.

## Lifecycle and States

Calendar management follows a default lifecycle with the states active and archived.

- **active:** The calendar is enforced. SLA policies and any other consumer evaluate their clock against the schedule and holidays of this calendar.
- **archived:** The calendar is read-only. Existing references keep working for historicization purposes, but the calendar is no longer offered when selecting a calendar from a form.

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                       KleeneStar Calendar State Diagram                              ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║                               ┌───────────────────┐                                  ║
║                               │     archive       │                                  ║
║                      new  ╔════════╗         ┌────▼─────┐                            ║
║                        ───► active ║         │ archived │                            ║
║                           ╚═══▲════╝         └────┬─────┘                            ║
║                               │     restore       │                                  ║
║                               └───────────────────┘                                  ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

## Data Model and Relationships

A calendar is uniquely bound to a class. Its weekly schedule is materialized as exactly seven `BusinessHourSlot` rows (one per `DayOfWeek`); its public-holiday list is stored as `Holiday` rows. The relationship to the consuming `SlaPolicy` is by foreign key (`SlaPolicy.CalendarId`) which is set to `null` if the referenced calendar is deleted.

- Key attributes: id (stable Guid), name (unique per class), description, time zone (IANA, e.g. `Europe/Berlin`), region tag (e.g. `DE`, `DE-BW`, `US-CA`), state, isDefault flag, icon.
- Schedule: seven `BusinessHourSlot` entries with day, enabled flag, start/end time.
- Holidays: list of `Holiday` entries with date, name, region tag, enabled flag.

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                       KleeneStar Calendar Data Model                                 ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║       ┌───────────┐ 1                * ┌──────────┐ 1            7 ┌───────────┐     ║
║       │ Workspace ├────────────────────► Class    ◄────────────────┤ Business  │     ║
║       └───────────┘                    └────┬─────┘                │ HourSlot  │     ║
║                                             │ 1                    └─────▲─────┘     ║
║                                             │                            │ 7         ║
║                                             │ *                          │           ║
║                                       ┌─────▼────┐ 1              * ┌────┴────┐      ║
║                                       │ Calendar ├──────────────────► Holiday │      ║
║                                       └─────▲────┘                  └─────────┘      ║
║                                             │ 0,1                                    ║
║                                             │                                        ║
║                                       ┌─────┴─────┐                                  ║
║                                       │ SlaPolicy │                                  ║
║                                       └───────────┘                                  ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

## Software Architecture

The `CalendarManager` mirrors the architecture of the other class-bound managers. It is a WebExpress component registered via reflection from `KleeneStar.Core.WebManager.CalendarManager`. The public API is exposed via the `ICalendarManager` interface and resolved by callers through `CoreHub.CalendarManager`. Persistence is delegated to `ModelHub.Calendar` (the partial class that holds the calendar query, insert, update, and delete code), which in turn talks to the `KleeneStarDbContext`. Child collections (`BusinessHours`, `Holidays`) are managed transactionally with the parent calendar.

A reactive event surface is provided through the `CalendarAdded`, `CalendarUpdated`, and `CalendarRemoved` events, which the SLA manager and the UI can subscribe to without depending on the manager directly. The REST and HTML surface is delivered through the `WebFragment/Calendar/` fragments and the `WWW/Api/_1_/Calendars/` endpoints.

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
║    ┌----------------┤ ICalendarManager                      │                        ║
║    ¦                ├───────────────────────────────────────┤                        ║
║    ¦                │ CalendarAdded:Event                   │                        ║
║    ¦                │ CalendarUpdated:Event                 │                        ║
║    ¦                │ CalendarRemoved:Event                 │                        ║
║    ¦              1 ├───────────────────────────────────────┤                        ║
║    ¦                │ GetCalendar(Guid):Calendar            │                        ║
║    ¦                │ GetCalendars(ClassId):                │                        ║
║    ¦                │   IEnumerable<Calendar>               │                        ║
║    ¦                │ GetCalendars(IQuery):                 │                        ║
║    ¦                │   IEnumerable<Calendar>               │                        ║
║    ¦                │ Add(Calendar):ICalendarManager        │                        ║
║    ¦                │ Update(Calendar):ICalendarManager     │                        ║
║    ¦                │ Remove(Guid):ICalendarManager         │                        ║
║    ¦                └────────────────Δ──────────────────────┘                        ║
║    ¦                                 ¦                                               ║
║    ¦ create         ┌────────────────┴──────────────────────┐                        ║
║    └----------------► Calendar                              │                        ║
║                     ├───────────────────────────────────────┤                        ║
║                     │ Id:Guid                               │                        ║
║                     │ Name:String                           │                        ║
║                     │ Description:String                    │                        ║
║                     │ TimeZone:String  (IANA)               │                        ║
║                     │ Region:String                         │                        ║
║                     │ State:CalendarState                   │                        ║
║                     │ IsDefault:Bool                        │                        ║
║                     │ Class:Class                           │                        ║
║                     │ Created:DateTime                      │                        ║
║                     │ Updated:DateTime                      │  ┌─────────────────┐   ║
║                     │ BusinessHours:                        │  │ <<Enum>>        │   ║
║                     │   IEnumerable<BusinessHourSlot>       │  │ CalendarState   │   ║
║                     │ Holidays:IEnumerable<Holiday>         │  ├─────────────────┤   ║
║                     └───────────────────────────────────────┘  │ Active          │   ║
║                                                                │ Archived        │   ║
║                                                                └─────────────────┘   ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### BusinessHourSlot

`BusinessHourSlot` stores one entry per weekday. The `Enabled` flag distinguishes working days from weekends or off days; when disabled, the `StartTime`/`EndTime` are ignored. A unique index on `(CalendarId, DayOfWeek)` guarantees that the schedule of a calendar always has at most seven distinct entries.

|Attribute     |Type        |Notes
|--------------|------------|-----
|`Id`          |`Guid`      |Stable identifier
|`DayOfWeek`   |`DayOfWeek` |Monday … Sunday
|`Enabled`     |`Bool`      |Whether this day is a working day
|`StartTime`   |`TimeOnly`  |Inclusive start (local calendar time)
|`EndTime`     |`TimeOnly`  |Inclusive end (local calendar time)
|`CalendarId`  |`Guid`      |Owning calendar (FK, cascade-delete)

### Holiday

`Holiday` stores a single date on which the calendar pauses. Each entry carries its own region tag so a single calendar can mix global (e.g. `DE`) and regional (e.g. `DE-BW`) entries; the `Enabled` flag lets administrators temporarily switch holidays off without losing the entry.

|Attribute    |Type       |Notes
|-------------|-----------|-----
|`Id`         |`Guid`     |Stable identifier
|`Date`       |`DateOnly` |Calendar-local date
|`Name`       |`String`   |Display name, e.g. "New Year's Day"
|`Region`     |`String`   |Region tag, e.g. `DE` or `DE-BW`
|`Enabled`    |`Bool`     |Whether this holiday is enforced; defaults to true
|`CalendarId` |`Guid`     |Owning calendar (FK, cascade-delete)

## UI Concepts and Pages

The calendar management UI integrates into the class detail pages. From the class sidebar, the "Calendar" item opens the calendar overview for the active class. The page lists every calendar of the class along with its time zone, region, holiday count, and "Default" marker. Per-row actions open the edit/clone/delete forms in a modal.

### Calendar Management in Class Editing

Entry into calendar management occurs directly from the class detail page via the "Calendar" item in the class sidebar. The sidebar item is `ClassSidebarCalendarLinkFragment` and is registered with every class-bound page (`Class._classid_.Index`, `Fields._classid_.Index`, `Forms._classid_.Index`, `Priorities._classid_.Index`, `Workflows._classid_.Index`, `Statuses._classid_.Index`, `Slas._classid_.Index`, `Calendars._classid_.Index`, `Form._formid_.Index`, `Workflow._workflowid_.Index`, `Sla._slaid_.Index`, `Calendar._calendarid_.Index`).

### Calendar Management (Page)

This page is the central administrative view for all calendars of a selected class. The main area is rendered by the `CalendarViewFragment` (toggle group), which hosts the `CalendarViewTableFragment`. Quick-filter chips ("Active", "Archived", "Default") sit in the header along with an advanced-search input backed by the calendar WQL endpoint. New calendars are created via the "Add calendar" button.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insight ▼         [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Service Desk / Incident / Calendars                                              │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Calendars─────────────┐ ┌Calendars Content──────────────────────────────────────────┐║
║│                      │░│                                                           │║
║│  - All               │░│ My Calendars                       [Search] [+ Add Cal]   │║
║│  - Active            │░│ ─ Chips ─ [Active] [Archived] [Default]                   │║
║│  - Archived          │░│                                                           │║
║│  - Default           │░│ Name              | TimeZone     | Region | State | Hol.. │║
║│                      │░│-------------------|--------------|--------|-------|-------│║
║│                      │░│ Standard · …      | Europe/Berlin| DE     | Active|  9    │║
║│                      │░│ 24 / 7 · Always on| UTC          |        | Active|  0    │║
║│                      │░│ Night shift · 22… | Europe/Berlin| DE     | Active|  0    │║
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

### Calendar Management - New/Edit (Modal)

The "Add calendar" and "Edit calendar" modals share a form layout. The basic-properties form covers name, description, time zone, region, state, and the "Default" flag. The weekly schedule and the holiday list are managed in their own tabs in the same modal (the seven-day grid is materialized server-side as seven `BusinessHourSlot` rows; the holiday list as `Holiday` rows). All settings are applied in a single transaction via the calendar REST CRUD endpoint.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insight ▼         [+ AddObject]         [Search]    │║
║└─────╔CalendarAddEditModal════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Add calendar / Edit calendar                                         │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Cal..║│            Name*: [ Standard · Europe/Berlin                       ] │║─────┐║
║│     ║│      Description: [ Business hours Mon-Fri 08-18 …                 ] │║     │║
║│     ║│         TimeZone: [ Europe/Berlin                                 ▼] │║ Cal]│║
║│     ║│           Region: [ DE                                             ] │║     │║
║│     ║│            State: [ Active                                        ▼] │║-----│║
║│     ║│           Default: [✓]                                               │║Hol..│║
║│     ║│                                                                      │║-----│║
║│     ║│ ┌Schedule────────────────────────────────────────────────────────┐   │║  9  │║
║│     ║│ │ Mon [✓] 08:00–18:00     Fri [✓] 08:00–18:00                    │   │║  0  │║
║│     ║│ │ Tue [✓] 08:00–18:00     Sat [ ] —                              │   │║  0  │║
║│     ║│ │ Wed [✓] 08:00–18:00     Sun [ ] —                              │   │║     │║
║│     ║│ │ Thu [✓] 08:00–18:00                                            │   │║t ›  │║
║│     ║│ └────────────────────────────────────────────────────────────────┘   │║     │║
║│     ║│                                                                      │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                       [Save] [Cancel]  ║     │║
║└─────╚════════════════════════════════════════════════════════════════════════╝─────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Calendar Management - Clone (Modal)

Cloning a calendar reuses the weekly schedule and holiday list of an existing entry. The new calendar starts in `Active` state with the suggested name `"<original> (Copy)"`. The "Default" flag is intentionally NOT cloned: only one default calendar per class is supported.

### Calendar Management - Delete (Modal)

Deleting a calendar is irreversible. The modal warns that every `SlaPolicy` referencing the calendar will have its `CalendarId` set to `null` (and therefore fall back to the class default calendar). A confirmation entry of the calendar name is required before the "Delete" button becomes active.

## Sitemap Calendar Management

|Path                                                                                |Page              |Description
|------------------------------------------------------------------------------------|------------------|-------------------------------------------------------------
|`/workspaces/{workspaceKey}/classes/{classKey}/calendars`                           |Calendar mgmt     |Central overview of all calendars of a class.
|`/workspaces/{workspaceKey}/classes/{classKey}/calendars/add`                       |Calendar creation |Modal for creating a new calendar within the class.
|`/workspaces/{workspaceKey}/classes/{classKey}/calendars/{calendarKey}`             |Calendar detail   |Detail view of a single calendar with schedule and holidays.
|`/workspaces/{workspaceKey}/classes/{classKey}/calendars/{calendarKey}/edit`        |Calendar edit     |Modal for changing scalar properties, schedule, and holidays.
|`/workspaces/{workspaceKey}/classes/{classKey}/calendars/{calendarKey}/clone`       |Calendar clone    |Modal for duplicating an existing calendar.
|`/workspaces/{workspaceKey}/classes/{classKey}/calendars/{calendarKey}/delete`      |Calendar delete   |Modal for confirming permanent removal.

## API Interfaces (REST Endpoints) - Calendar Management

For programmatic interaction, automation, and form rendering, calendars are exposed via a versioned REST API rooted at `/api/1/calendars`. Endpoints use JSON, follow REST conventions, and are protected by the standard **KleeneStar** authentication and authorization stack.

|Endpoint                                                  |HTTP Method |Description
|----------------------------------------------------------|------------|------------------------------------------------------------
|`/api/1/calendars`                                        |GET         |Lists all calendars, paginated and filterable.
|`/api/1/calendars`                                        |POST        |Creates a new calendar. Requires `name`, `classId`, and (optionally) the schedule and holiday list.
|`/api/1/calendars/{calendarKey}`                          |GET         |Returns detail of a calendar including schedule and holidays.
|`/api/1/calendars/{calendarKey}`                          |PUT         |Updates the calendar in one transaction.
|`/api/1/calendars/{calendarKey}`                          |DELETE      |Permanently deletes the calendar. Existing `SlaPolicy.CalendarId` references are nulled.
|`/api/1/calendars/state`                                  |GET         |REST selection of `CalendarState` values (used by the state dropdown).
|`/api/1/calendars/timezone`                               |GET         |REST selection of common IANA time zones (used by the timezone dropdown).
|`/api/1/calendars/uniquename`                             |GET         |Validates that a candidate name is available within the class.
|`/api/1/calendars/wql`                                    |GET         |WQL prompt suggestions for the calendar advanced search.
|`/api/1/calendars/{classKey}/table`                       |GET         |Table-row backing for the calendar view (class-scoped).
|`/api/1/calendars/{classKey}/quickfilter`                 |GET         |Quick-filter chips ("Active", "Archived", "Default").

Standard HTTP status codes apply: `200`/`201`/`204` for success, `400` for validation errors (e.g. duplicate name), `401` for unauthenticated, `403` for forbidden, `404` for unknown calendar/class.

## Calendar Events

The `CalendarManager` publishes the following events via the **WebExpress** `EventManager`. Other managers (notably `SlaManager`) and UI components can subscribe to react to changes:

|Event Name         |Description
|-------------------|----------------------------------------------------------------
|`CalendarAdded`    |Triggered when a new calendar has been added to a class.
|`CalendarUpdated`  |Signals changes to scalar properties, the weekly schedule, or the holiday list.
|`CalendarRemoved`  |Indicates permanent deletion of a calendar.

Each event payload carries the affected `Calendar` entity (with its child collections), allowing subscribers to invalidate caches, re-evaluate downstream SLA timers, or update the UI without re-querying.

## Conclusion

This document describes the calendar concept in **KleeneStar** as the single source of truth for working-hours and holidays. The reference implementation comprises the `Calendar`, `BusinessHourSlot`, and `Holiday` entities, the `CalendarManager` component, a versioned REST API, native WebExpress pages and fragments, and a seeded catalogue that the SLA management feature directly consumes through the `SlaPolicy.CalendarId` foreign key. The model is intentionally narrow and composable so that future time-sensitive features (scheduled jobs, on-call rosters, change windows) can reference the same calendar definitions without duplicating schedule logic.
