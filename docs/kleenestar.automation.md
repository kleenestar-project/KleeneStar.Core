![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Automation

The workflow already knows rules: guards decide whether a transition is offered, validators whether it may complete, post functions what follows it. They act at one moment - a person moving an object. The **automation platform** generalizes them: a rule reacts to *any* event, to a condition, or to a point in time, and performs actions on the object - for every class, not only for tickets.

## A rule

|Part        |Stored as                     |Meaning
|------------|------------------------------|---------------------------------------------------------------
|Scope       |`WorkspaceId`, `ClassId`      |the objects of a workspace, or of one of its classes
|Trigger     |`Trigger`, `TriggerParameter` |a key of the open trigger catalog and an optional parameter
|Condition   |`Condition`                   |a WQL expression the object has to match when the event happens
|Actions     |`Actions`                     |a script, one action per line
|Identity    |`RunAsId` (else the creator)  |the author of the rule's comments, the actor of its moves
|State       |`Active` / `Disabled`         |

**Triggers** (`WebAutomation/AutomationTriggerCatalog`, open - a plugin registers more):

|Key                  |Raised by                                    |Parameter
|---------------------|---------------------------------------------|----------------------------------------------
|`object.created`     |a genesis commit                             |-
|`object.updated`     |any other commit                             |the attributes that changed, comma-separated
|`transition.executed`|a completed workflow move                    |the target state
|`comment.added`      |a new comment                                |`public` or `internal`
|`sla.breached`       |the scheduler, every 5 minutes               |the target or agreement; once per target and cycle
|`sla.atrisk`         |the scheduler, every 5 minutes               |the target or agreement; once per target and cycle, when the warning threshold (80 %) is passed
|`integration.received`|an incoming webhook (`kleenestar.integration.md`)|the event or the webhook
|`approval.requested` / `approval.completed` / `approval.overdue`|approvals (`kleenestar.approval.md`)|policy; outcome; step
|`schedule`           |the scheduler                                |the interval in minutes; once per object and interval

An empty parameter reacts to every occurrence. The condition is evaluated **when the event happens**, against the object as the event left it: a rule reacts to what happened, not to what the object looks like by the time a worker gets to it. A condition that stopped compiling matches nothing.

**Actions** (`WebAutomation/AutomationActionCatalog`, open - an integration registers its calls):

|Line                                              |Does
|--------------------------------------------------|-------------------------------------------------------------
|`assign <account \| reporter \| unassigned>`        |sets the assignee
|`transition <state>`                              |moves the object through the workflow - guards, validators and post functions included; already there counts as done
|`set <field>=<value>`                             |writes a field value
|`comment [internal:] <text>`                      |writes a comment in the rule's name; `internal:` reaches the service team only
|`notify <assignee, reporter, watchers or accounts>: <text>` |puts a notification into each recipient's notification center, linking the object
|`subtask <WORKSPACE>/<class>: <summary>`          |files a sub-task in any workspace, joined by the aggregating relation
|`escalate [priority]`                             |raises the priority one step, or to the named one
|`postfunction <key>`                              |runs a registered workflow post function
|`archive [days]`                                  |archives the object - with days only when it is done and unchanged that long (retention rules, `kleenestar.recovery.md`)
|`webhook <name>` / `approval <policy>`            |calls an outgoing webhook (`kleenestar.integration.md`) / requests an approval (`kleenestar.approval.md`)

Blank lines and lines starting with `#` are ignored. A rule is **validated in full when it is saved** - the trigger, the condition, every line against the class - and refused with each problem and its line (`IAutomationManager.Validate`); a rule that could not run is never stored.

## Reliable execution

`AutomationManager` captures the events (`CommitManager.CommitAdded`, `WorkflowManager.TransitionExecuted`, `CommentManager.CommentAdded`, the scheduler) and writes an `AutomationJob` per matching rule **before** anything is done about it.

- **Persistent.** A host that goes down between an event and its consequence does the work after the restart.
- **Deduplicated.** `AutomationJob.Deduplication` (`<rule>:<occurrence>`) is unique: an event delivered twice - a replayed message, a retried webhook, an overlapping scheduler run - queues nothing the second time. An integration raising events passes a stable occurrence key.
- **Leased.** `AutomationWorker` (started with the application; every 5 s for jobs, every minute for schedules, hourly housekeeping) claims due jobs for 5 minutes under the optimistic concurrency of `AutomationJob.Version`; several replicas share the queue safely, and a job whose worker died is claimed again when the lease runs out.
- **Resumable.** `CompletedActions` is written after each line; a resumed job continues after the last finished action. Actions tolerate being run again: setting a value or a state is idempotent by nature, `comment` checks for its own text, `subtask` creates under the job's creation key (`ObjectCreationRun`, see `kleenestar.template.md`). A notification may arrive twice after a crash - accepted.
- **Retried, then parked.** A failure is retried after 30 s, 2 min, 8 min, 32 min (`Backoff`); after 5 attempts the job is `Failed`, keeps `LastError`, raises `JobFailed` (audited as a warning) and waits on the automation page until somebody runs it again - after the actions it already did.
- **Housekeeping.** Finished jobs are kept 30 days.

Jobs run **as the system** (unrestricted - a rule acts on every object of its scope), with the writes **attributed** to the rule's identity (`BeginIdentity`), recorded in the audit log with origin `Automation`, and without toasts.

**Scale.** A scheduler walks the objects of a rule's scope once per interval: the rule's condition is compiled once per version of the rule, and the scheduler queues the jobs of its own rule directly - another scheduled rule runs on its own interval. The load test (`UnitTestLoad`, `KLEENESTAR_LOAD=1`, on a migrated SQLite file) holds these paths to budgets at 60,000 objects - a year of a busy service desk. Measured (2026-10-23, developer machine): seeding 13 s, a scheduler pass over all objects 1.1 s, 2,000 events raised 6 s (3 ms each), the 2,060 resulting jobs worked off in 44 s (21 ms each, a comment written per job), the team load of 200 people 0.9 s, the CSV export of all objects 2.4 s. The run found and fixed two hot spots: schedulers raising one event per object (minutes, now direct enqueueing with a once-compiled condition) and the done test of the planning loads (574 s, now `DoneJudge` reading each class's states in one query).

**Loops.** What a rule does raises events of its own. An event raised while a job runs belongs to it: it never triggers the same rule again, and a chain of rules reacting to each other's consequences stops after 3 links (`MaxDepth`).

## Surfaces

- **Automation page** `WWW/Automation/{workspacekey}` (workspace cog → *Automation*): the rules, and the jobs with the failed ones first and *Run again*. `Rule?rule=<id>` writes a rule (trigger, parameter, class, WQL condition, script with the syntax of every registered action as help, act as, state); `Delete?rule=` and `Retry?job=` confirm. The area is workspace administration (`RouteAuthorization`), the endpoints ask `MayAdministerWorkspace`; a job names its object only where the caller may read it.
- **REST** `/api/1/automation/{workspacekey}`: `GET` rules, `POST` add/change (with `id`), `POST …/delete {id}`, `GET …/jobs[?state=failed]`, `POST …/jobs {id}` to run a failed job again. Refusals answer `{ "message" }`.

## Open

A visual rule builder, rule templates per kind (ITSM escalations), an execution log per action, and conditions on custom field values (WQL works on the object's own attributes) are not part of this first version.
