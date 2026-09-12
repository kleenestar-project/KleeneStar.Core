using KleeneStar.Core.WebWorkflow;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Workflows
{
    /// <summary>
    /// Provides functionality for editing workflows through a REST API interface.
    /// Supports retrieval of workflow structure including states and transitions
    /// for the visual workflow editor control.
    /// </summary>
    /// <remarks>
    /// The rules a transition carries - its guards, validators and post functions - are
    /// configured here as well. The catalogs offered in the editor's pickers are the rules the
    /// <see cref="CoreHub.WorkflowGuardManager"/>, <see cref="CoreHub.WorkflowValidatorManager"/>
    /// and <see cref="CoreHub.WorkflowPostFunctionManager"/> registries know, and what the editor
    /// lists on a transition is written back as the expressions <see cref="WebManager.WorkflowManager"/>
    /// evaluates (<see cref="Transition.GuardExpression"/>, <see cref="Transition.ValidatorExpression"/>,
    /// <see cref="Transition.PostFunctionKeys"/>). The rule <em>key</em> travels in the entry's
    /// <c>type</c>, because that is the one field the editor preserves from a picked catalog
    /// entry; the label is only shown.
    /// </remarks>
    [Title("Workflow editor")]
    public sealed class WorkflowEditor : RestApiWorkflow
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public WorkflowEditor()
        {
        }

        /// <summary>
        /// Creates a query context backed by the application's database.
        /// </summary>
        /// <returns>The shared <see cref="KleeneStarDbContext"/>.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the header of the workflow identified by the specified identifier.
        /// </summary>
        /// <remarks>
        /// The base class takes only the header from here and sources the states and transitions
        /// from <see cref="RetrieveStates"/> and <see cref="RetrieveTransitions"/>, so this query
        /// stays deliberately shallow: the update handler calls it twice per save to run the
        /// optimistic concurrency check and to read the version back.
        /// </remarks>
        /// <param name="workflowId">
        /// The unique identifier of the workflow to retrieve.
        /// </param>
        /// <param name="context">
        /// The query context providing access to the underlying data store. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The current API request. Cannot be null.
        /// </param>
        /// <returns>
        /// A <see cref="RestApiWorkflowResult"/> representing the workflow, or <c>null</c>
        /// when no matching workflow exists, which the base class answers with 404.
        /// </returns>
        protected override RestApiWorkflowResult Retrieve(string workflowId, IQueryContext context, IRequest request)
        {
            if (!Guid.TryParse(workflowId, out var guid))
            {
                return null;
            }

            var db = context as KleeneStarDbContext;
            var workflow = db.Workflows
                .AsNoTracking()
                .FirstOrDefault(w => w.Id == guid);

            if (workflow is null)
            {
                return null;
            }

            return new RestApiWorkflowResult()
            {
                Id = workflow.Id.ToString(),
                Name = workflow.Name,
                Description = workflow.Description,
                State = workflow.State.ToString(),
                Version = GetVersion(workflow)
            };
        }

        /// <summary>
        /// Builds the revision token the editor round-trips for optimistic concurrency. The
        /// modification timestamp serves as the revision, because every write through this
        /// endpoint advances it; a save presenting an older token is rejected with 409 rather
        /// than overwriting the newer revision.
        /// </summary>
        /// <param name="workflow">The workflow whose revision is requested. Cannot be null.</param>
        /// <returns>The revision token.</returns>
        private static string GetVersion(Model.Entities.Workflow workflow)
        {
            return workflow.Updated.Ticks.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Persists the workflow definition delivered by the editor's autosave.
        /// </summary>
        /// <remarks>
        /// Only what the data model can hold is written back: the state labels and the whole
        /// transition set. The canvas positions, the state colors and the transition rules have no
        /// counterpart in the schema, and a state the editor added carries no status category, so
        /// none of them are persisted.
        /// </remarks>
        /// <param name="workflowId">
        /// The unique identifier of the workflow to update.
        /// </param>
        /// <param name="workflow">
        /// The workflow definition to persist, carrying the states and transitions in the same shape
        /// the GET request delivers.
        /// </param>
        /// <param name="context">
        /// The query context providing access to the underlying data store. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The current API request. Cannot be null.
        /// </param>
        protected override void Update(string workflowId, RestApiWorkflowResult workflow, IQueryContext context, IRequest request)
        {
            if (workflow is null || !Guid.TryParse(workflowId, out var guid))
            {
                return;
            }

            var db = context as KleeneStarDbContext;
            var entity = db?.Workflows
                .Include(w => w.WorkflowStatuses)
                    .ThenInclude(ws => ws.Status)
                .Include(w => w.Transitions)
                .FirstOrDefault(w => w.Id == guid);

            if (entity is null)
            {
                return;
            }

            var timestamp = DateTime.UtcNow;

            UpdateStates(db, entity, workflow.States, timestamp);
            UpdateTransitions(db, entity, workflow.Transitions, timestamp);

            // the modification timestamp is the revision the concurrency check compares, so it
            // has to advance on every write, including one that only moved a transition
            entity.Updated = timestamp;

            db.SaveChanges();
        }

        /// <summary>
        /// Reconciles the states of the workflow with the ones posted by the editor: a known state
        /// has its label, canvas position and entry/end marks applied, an unknown one is created as
        /// a status of the workflow's class, and one the editor no longer carries stops taking part
        /// in the workflow.
        /// </summary>
        /// <remarks>
        /// Dropping a state removes only its participation, never the status itself: a status is
        /// defined per class and may be referenced by objects and by other workflows. A status that
        /// objects currently sit in keeps its participation even so, because removing it would
        /// leave those objects pointing at a state the workflow no longer knows.
        /// </remarks>
        /// <param name="db">The database context. Cannot be null.</param>
        /// <param name="workflow">The tracked workflow the states belong to. Cannot be null.</param>
        /// <param name="states">The states as posted by the editor.</param>
        /// <param name="timestamp">The modification timestamp to stamp on a changed status.</param>
        private static void UpdateStates(KleeneStarDbContext db, Model.Entities.Workflow workflow, IEnumerable<RestApiWorkflowState> states, DateTime timestamp)
        {
            if (states is null || workflow.WorkflowStatuses is null)
            {
                return;
            }

            // the position and the marks sit on the pairing, because a status is defined per
            // class and can take part in several workflows with a different layout in each
            var participations = workflow.WorkflowStatuses
                .Where(ws => ws.Status is not null)
                .ToDictionary(ws => ws.StatusId);

            // the status name is unique per class, so a rename or an insert must dodge the names
            // of its siblings
            var taken = db.Statuses
                .Where(s => s.ClassId == workflow.ClassId)
                .Select(s => s.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var retained = new HashSet<Guid>();

            foreach (var state in states)
            {
                if (state is null)
                {
                    continue;
                }

                var label = state.Label?.Trim();

                if (Guid.TryParse(state.Id, out var stateId) && participations.TryGetValue(stateId, out var participation))
                {
                    retained.Add(stateId);

                    participation.X = state.X;
                    participation.Y = state.Y;
                    participation.IsStart = state.IsStart;
                    participation.IsEnd = state.IsEnd;

                    var status = participation.Status;

                    if (!string.IsNullOrEmpty(label) && !label.Equals(status.Name, StringComparison.Ordinal))
                    {
                        taken.Remove(status.Name);
                        status.Name = MakeUnique(label, taken);
                        status.Updated = timestamp;
                        taken.Add(status.Name);
                    }

                    continue;
                }

                // the editor mints its own id for a state it created and has no way to ask for a
                // category, so the status is inserted under a fresh id in the category nominated
                // as the default
                var category = db.StatusCategories.FirstOrDefault(c => c.IsDefault);

                if (category is null)
                {
                    continue;
                }

                var added = new Model.Entities.Status()
                {
                    Name = MakeUnique(string.IsNullOrEmpty(label) ? "State" : label, taken),
                    State = StatusState.Active,
                    ClassId = workflow.ClassId,
                    CategoryId = category.Id,
                    Created = timestamp,
                    Updated = timestamp
                };

                taken.Add(added.Name);

                var participationAdded = new WorkflowStatus()
                {
                    WorkflowId = workflow.Id,
                    StatusId = added.Id,
                    Status = added,
                    X = state.X,
                    Y = state.Y,
                    IsStart = state.IsStart,
                    IsEnd = state.IsEnd
                };

                db.Statuses.Add(added);
                workflow.WorkflowStatuses.Add(participationAdded);
                retained.Add(added.Id);
            }

            var dropped = workflow.WorkflowStatuses
                .Where(ws => !retained.Contains(ws.StatusId))
                .Where(ws => !IsOccupied(db, workflow, ws.Status))
                .ToList();

            foreach (var participation in dropped)
            {
                workflow.WorkflowStatuses.Remove(participation);
            }
        }

        /// <summary>
        /// Determines whether any object currently sits in the given status. The workflow field
        /// stores its value loosely - as the status id or as a slug of its name - so both spellings
        /// are compared, mirroring how the object views resolve it.
        /// </summary>
        /// <param name="db">The database context. Cannot be null.</param>
        /// <param name="workflow">The workflow the status takes part in. Cannot be null.</param>
        /// <param name="status">The status to test. Cannot be null.</param>
        /// <returns>True when at least one object references the status.</returns>
        private static bool IsOccupied(KleeneStarDbContext db, Model.Entities.Workflow workflow, Model.Entities.Status status)
        {
            var fields = db.Fields
                .Where(f => f.ClassId == workflow.ClassId && f.FieldType == FieldType.Workflow && f.WorkflowId == workflow.Id)
                .Select(f => f.Id)
                .ToList();

            if (fields.Count == 0)
            {
                return false;
            }

            var id = status.Id.ToString();
            var normalized = Normalize(status.Name);

            return db.Values
                .Where(v => fields.Contains(v.FieldId) && v.Data != null)
                .Select(v => v.Data)
                .ToList()
                .Any(data => string.Equals(data, id, StringComparison.OrdinalIgnoreCase) || Normalize(data) == normalized);
        }

        /// <summary>
        /// Reduces a string to its lower-cased alphanumeric characters, so a loosely formatted
        /// status slug can be compared against a status name.
        /// </summary>
        /// <param name="value">The value to normalise.</param>
        /// <returns>The normalised string.</returns>
        private static string Normalize(string value)
        {
            return new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        /// <summary>
        /// Reconciles the transitions of the workflow with the ones posted by the editor: a known
        /// transition is updated, an unknown one is inserted and one the editor no longer carries is
        /// deleted. A transition whose endpoints are not states of the workflow is skipped.
        /// </summary>
        /// <param name="db">The database context. Cannot be null.</param>
        /// <param name="workflow">The tracked workflow the transitions belong to. Cannot be null.</param>
        /// <param name="transitions">The transitions as posted by the editor.</param>
        /// <param name="timestamp">The modification timestamp to stamp on a changed transition.</param>
        private static void UpdateTransitions(KleeneStarDbContext db, Model.Entities.Workflow workflow, IEnumerable<RestApiWorkflowTransition> transitions, DateTime timestamp)
        {
            if (transitions is null)
            {
                return;
            }

            // read from the participations rather than from the skip navigation, so a state the
            // same save has just created is already a valid endpoint and one it dropped is not
            var stateIds = (workflow.WorkflowStatuses ?? [])
                .Select(ws => ws.StatusId)
                .ToHashSet();

            var existing = (workflow.Transitions ?? [])
                .ToDictionary(t => t.Id);

            // the transition name is unique per workflow, so both a rename and an insert must dodge
            // the names already in use
            var taken = existing.Values
                .Select(t => t.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var retained = new HashSet<Guid>();

            foreach (var transition in transitions)
            {
                if (!Guid.TryParse(transition?.From, out var sourceId) ||
                    !Guid.TryParse(transition?.To, out var targetId) ||
                    !stateIds.Contains(sourceId) ||
                    !stateIds.Contains(targetId))
                {
                    continue;
                }

                var label = transition.Label?.Trim();
                var description = transition.Description?.Trim();

                if (Guid.TryParse(transition.Id, out var transitionId) && existing.TryGetValue(transitionId, out var current))
                {
                    retained.Add(transitionId);

                    if (!string.IsNullOrEmpty(label) && !label.Equals(current.Name, StringComparison.Ordinal))
                    {
                        taken.Remove(current.Name);
                        current.Name = MakeUnique(label, taken);
                        taken.Add(current.Name);
                    }

                    current.Description = description;
                    current.SourceId = sourceId;
                    current.TargetId = targetId;
                    current.Color = transition.Color;
                    current.DashArray = transition.DashArray;
                    current.Waypoints = ToWaypoints(transition.Waypoints);
                    current.GuardExpression = ToExpression(transition.Guards?.Select(x => RuleKey(x?.Type)), current.GuardExpression);
                    current.ValidatorExpression = ToExpression(transition.Validators?.Select(x => RuleKey(x?.Type)), current.ValidatorExpression);
                    current.PostFunctionKeys = ToKeys(transition.PostFunctions?.Select(x => RuleKey(x?.Type)));
                    current.Updated = timestamp;

                    continue;
                }

                // the editor mints its own id for a new transition, so it is inserted under a fresh one
                var added = new Transition()
                {
                    Name = MakeUnique(string.IsNullOrEmpty(label) ? "Transition" : label, taken),
                    Description = description,
                    State = TransitionState.Active,
                    WorkflowId = workflow.Id,
                    SourceId = sourceId,
                    TargetId = targetId,
                    Color = transition.Color,
                    DashArray = transition.DashArray,
                    Waypoints = ToWaypoints(transition.Waypoints),
                    GuardExpression = ToExpression(transition.Guards?.Select(x => RuleKey(x?.Type)), null),
                    ValidatorExpression = ToExpression(transition.Validators?.Select(x => RuleKey(x?.Type)), null),
                    PostFunctionKeys = ToKeys(transition.PostFunctions?.Select(x => RuleKey(x?.Type))),
                    Created = timestamp,
                    Updated = timestamp
                };

                taken.Add(added.Name);
                db.Transitions.Add(added);
            }

            db.Transitions.RemoveRange(existing.Values.Where(t => !retained.Contains(t.Id)));
        }

        /// <summary>
        /// Converts the posted waypoints into their stored form. A transition the user routed
        /// straight again arrives with an empty list, which clears the stored route.
        /// </summary>
        /// <param name="waypoints">The waypoints as posted by the editor.</param>
        /// <returns>The waypoints to store, never null.</returns>
        private static List<TransitionWaypoint> ToWaypoints(IEnumerable<RestApiWorkflowWaypoint> waypoints)
        {
            return waypoints?
                .Where(w => w is not null)
                .Select(w => new TransitionWaypoint() { X = w.X, Y = w.Y })
                .ToList() ?? [];
        }

        /// <summary>
        /// Returns the name itself when it is still free, or the first numbered variant of it that
        /// is, so the unique index over the name is never violated.
        /// </summary>
        /// <param name="name">The desired name.</param>
        /// <param name="taken">The names already in use.</param>
        /// <returns>A name that is not part of the given set.</returns>
        private static string MakeUnique(string name, ISet<string> taken)
        {
            if (!taken.Contains(name))
            {
                return name;
            }

            for (var suffix = 2; ; suffix++)
            {
                var candidate = $"{name} ({suffix})";

                if (!taken.Contains(candidate))
                {
                    return candidate;
                }
            }
        }

        /// <summary>
        /// Retrieves the available states for the workflow identified by the specified identifier.
        /// </summary>
        /// <param name="workflowId">
        /// The unique identifier of the workflow whose states are to be retrieved.
        /// </param>
        /// <param name="context">
        /// The query context providing access to the underlying data store. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The current API request. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of <see cref="RestApiWorkflowState"/> representing the
        /// active states of the workflow. The collection may be empty if no states are defined.
        /// </returns>
        protected override IEnumerable<RestApiWorkflowState> RetrieveStates(string workflowId, IQueryContext context, IRequest request)
        {
            if (!Guid.TryParse(workflowId, out var guid))
            {
                return [];
            }

            var db = context as KleeneStarDbContext;
            var workflow = db.Workflows
                .Include(w => w.WorkflowStatuses)
                    .ThenInclude(ws => ws.Status)
                        .ThenInclude(s => s.Category)
                .AsNoTracking()
                .FirstOrDefault(w => w.Id == guid);

            if (workflow?.WorkflowStatuses is null)
            {
                return [];
            }

            return workflow.WorkflowStatuses
                .Where(ws => ws.Status is not null && ws.Status.State == StatusState.Active)
                .Select(ws => new RestApiWorkflowState()
                {
                    Id = ws.Status.Id.ToString(),
                    Label = ws.Status.Name,
                    BackgroundColor = ws.Status.Category?.Color ?? "#6c757d",
                    ForegroundColor = "#ffffff",
                    // the status symbol is a picture, so it belongs in Image; a URL placed in
                    // Icon would be set as a CSS class on an empty element and render nothing
                    Image = ws.Status.Icon?.Uri?.ToString(),
                    X = ws.X,
                    Y = ws.Y,
                    IsStart = ws.IsStart,
                    IsEnd = ws.IsEnd
                })
                .ToList();
        }

        /// <summary>
        /// Retrieves the transitions for the workflow identified by the specified identifier.
        /// </summary>
        /// <param name="workflowId">
        /// The unique identifier of the workflow whose transitions are to be retrieved.
        /// </param>
        /// <param name="context">
        /// The query context providing access to the underlying data store. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The current API request. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of <see cref="RestApiWorkflowTransition"/> representing the
        /// active transitions of the workflow. The collection may be empty if no transitions are defined.
        /// </returns>
        protected override IEnumerable<RestApiWorkflowTransition> RetrieveTransitions(string workflowId, IQueryContext context, IRequest request)
        {
            if (!Guid.TryParse(workflowId, out var guid))
            {
                return [];
            }

            var db = context as KleeneStarDbContext;
            var transitions = db.Transitions
                .Where(t => t.WorkflowId == guid && t.State == TransitionState.Active)
                .AsNoTracking()
                .ToList();

            return transitions.Select(t => new RestApiWorkflowTransition()
            {
                Id = t.Id.ToString(),
                From = t.SourceId.ToString(),
                To = t.TargetId.ToString(),
                Label = t.Name,
                Description = t.Description,
                Color = t.Color,
                DashArray = t.DashArray,
                Waypoints = t.Waypoints?
                    .Select(w => new RestApiWorkflowWaypoint() { X = w.X, Y = w.Y })
                    .ToList(),
                Guards = Terms(t.GuardExpression)
                    .Select(key => new RestApiWorkflowGuard { Id = key, Type = key, Label = RuleLabel(CoreHub.WorkflowGuardManager?.Get(key), key, request) })
                    .ToList(),
                Validators = Terms(t.ValidatorExpression)
                    .Select(key => new RestApiWorkflowValidator { Id = key, Type = key, Label = RuleLabel(CoreHub.WorkflowValidatorManager?.Get(key), key, request) })
                    .ToList(),
                PostFunctions = ToKeys(t.PostFunctionKeys)
                    .Select(key => new RestApiWorkflowPostFunction { Id = key, Type = key, Label = RuleLabel(CoreHub.WorkflowPostFunctionManager?.Get(key), key, request) })
                    .ToList()
            });
        }

        /// <summary>
        /// Reads the distinct terms of a rule expression, in order of appearance, regardless of
        /// how they are grouped.
        /// </summary>
        /// <remarks>
        /// The editor lists a transition's rules as one flat list, so a disjunction written by
        /// hand (<c>a;b|c</c>) is shown as the rules it mentions. <see cref="ToExpression"/> keeps
        /// such an expression as long as the list is not edited.
        /// </remarks>
        /// <param name="expression">The stored expression.</param>
        /// <returns>The keys the expression mentions.</returns>
        private static List<string> Terms(string expression)
        {
            return [.. WorkflowExpression.Parse(expression)
                .SelectMany(group => group)
                .Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>
        /// Writes the rule keys the editor lists on a transition as the expression the workflow
        /// evaluates: one conjunction, because a list of rules means all of them.
        /// </summary>
        /// <remarks>
        /// An expression the editor cannot express - a disjunction of several groups - is kept
        /// as it is while the list it was shown as is unchanged, so opening a transition in the
        /// editor does not flatten what an administrator wrote by other means.
        /// </remarks>
        /// <param name="keys">The keys as posted, or <see langword="null"/> when the payload
        /// carries no list.</param>
        /// <param name="stored">The expression currently stored.</param>
        /// <returns>The expression to store; <see langword="null"/> for an empty list.</returns>
        internal static string ToExpression(IEnumerable<string> keys, string stored)
        {
            var wanted = ToKeys(keys);

            if (wanted.Count == 0)
            {
                return null;
            }

            var current = Terms(stored);

            if (current.Count == wanted.Count && !current.Except(wanted, StringComparer.OrdinalIgnoreCase).Any())
            {
                return stored;
            }

            return WorkflowExpression.Serialize([wanted]);
        }

        /// <summary>
        /// Normalizes a list of rule keys: trimmed, non-empty, distinct, in the order given.
        /// </summary>
        /// <param name="keys">The keys, or <see langword="null"/>.</param>
        /// <returns>The keys, never <see langword="null"/>.</returns>
        internal static List<string> ToKeys(IEnumerable<string> keys)
        {
            return [.. (keys ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>
        /// Reads the rule key out of a rule entry the editor posted.
        /// </summary>
        /// <remarks>
        /// A picked catalog entry keeps the catalog's <c>type</c>, which is where the key is
        /// served. The <c>id</c> is the editor's own - <c>validators_1731…</c> - and is never
        /// read as a key: an entry without a type names no rule, and storing an editor id as
        /// one would block the transition on a rule that does not exist.
        /// </remarks>
        /// <param name="type">The entry's type.</param>
        /// <returns>The key, or <see langword="null"/> when the entry names none.</returns>
        private static string RuleKey(string type)
        {
            return string.IsNullOrWhiteSpace(type) ? null : type.Trim();
        }

        /// <summary>
        /// Resolves the label a rule is shown under: its translated label, or its key when the
        /// rule is not registered - so a transition that names a rule of an uninstalled plugin
        /// still shows which one.
        /// </summary>
        /// <param name="rule">The rule, or <see langword="null"/>.</param>
        /// <param name="key">The key the rule was addressed by.</param>
        /// <param name="request">The request, for the culture.</param>
        /// <returns>The label.</returns>
        private static string RuleLabel(IWorkflowRule rule, string key, IRequest request)
        {
            if (rule is null || string.IsNullOrWhiteSpace(rule.Label))
            {
                return key;
            }

            return request is null ? rule.Label : I18N.Translate(request, rule.Label);
        }

        /// <summary>
        /// Projects the registered rules of one kind onto the editor's catalog entries, in the
        /// order the rules ask to be listed.
        /// </summary>
        /// <typeparam name="TRule">The kind of rule.</typeparam>
        /// <typeparam name="TItem">The catalog entry type.</typeparam>
        /// <param name="rules">The registered rules.</param>
        /// <param name="request">The request, for the culture.</param>
        /// <param name="create">Builds an entry from a key and a label.</param>
        /// <returns>The catalog.</returns>
        private static List<TItem> Catalog<TRule, TItem>(IEnumerable<TRule> rules, IRequest request, Func<string, string, TItem> create)
            where TRule : IWorkflowRule
        {
            return [.. (rules ?? [])
                .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Key))
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => create(x.Key, RuleLabel(x, x.Key, request)))];
        }

        /// <summary>
        /// Retrieves the collection of validators to be applied for the specified workflow operation.
        /// </summary>
        /// <param name="workflowId">
        /// The unique identifier of the workflow operation for which validations are requested.
        /// </param>
        /// <param name="context">
        /// The query context that provides access to data and services relevant to the validation process.
        /// </param>
        /// <param name="request">
        /// The request object containing details about the current API request.
        /// </param>
        /// <returns>
        /// An enumerable collection of <see cref="RestApiWorkflowValidator"/> instances representing the 
        /// validations to apply. The collection may be empty if no validations are required.
        /// </returns>
        protected override IEnumerable<RestApiWorkflowValidator> RetrieveValidations(string workflowId, IQueryContext context, IRequest request)
        {
            return Catalog
            (
                CoreHub.WorkflowValidatorManager?.Rules,
                request,
                (key, label) => new RestApiWorkflowValidator { Id = key, Type = key, Label = label }
            );
        }

        /// <summary>
        /// Retrieves the collection of post functions associated with the specified identifier.
        /// </summary>
        /// <param name="workflowId">
        /// The unique identifier for which to retrieve post functions.
        /// </param>
        /// <param name="context">
        /// The query context that provides access to data and services required for retrieval.
        /// </param>
        /// <param name="request">
        /// The request information relevant to the retrieval operation.
        /// </param>
        /// <returns>
        /// An enumerable collection of post functions associated with the specified identifier. Returns 
        /// an empty collection if no post functions are found.
        /// </returns>
        protected override IEnumerable<RestApiWorkflowPostFunction> RetrievePostFunctions(string workflowId, IQueryContext context, IRequest request)
        {
            return Catalog
            (
                CoreHub.WorkflowPostFunctionManager?.Rules,
                request,
                (key, label) => new RestApiWorkflowPostFunction { Id = key, Type = key, Label = label }
            );
        }

        /// <summary>
        /// Retrieves the collection of workflow guards associated with the specified workflow.
        /// </summary>
        /// <param name="workflowId">
        /// The unique identifier of the workflow for which to retrieve guards.
        /// </param>
        /// <param name="context">
        /// The query context that provides access to relevant data and services during guard retrieval.
        /// </param>
        /// <param name="request">
        /// The request object containing details about the current API operation.
        /// </param>
        /// <returns>
        /// An enumerable collection of <see cref="RestApiWorkflowGuard"/> instances representing the guards 
        /// for the specified workflow. Returns an empty collection if no guards are defined.
        /// </returns>
        protected override IEnumerable<RestApiWorkflowGuard> RetrieveGuards(string workflowId, IQueryContext context, IRequest request)
        {
            return Catalog
            (
                CoreHub.WorkflowGuardManager?.Rules,
                request,
                (key, label) => new RestApiWorkflowGuard { Id = key, Type = key, Label = label }
            );
        }
    }
}
