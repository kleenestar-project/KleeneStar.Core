using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebWorkflow;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebAutomation.Actions
{
    /// <summary>
    /// The actions the platform ships: assigning, moving, setting, commenting, notifying,
    /// decomposing and escalating an object, and running a workflow post function.
    /// </summary>
    public static class StandardActions
    {
        /// <summary>
        /// Gets every standard action.
        /// </summary>
        public static IReadOnlyList<IAutomationAction> All { get; } =
        [
            new AssignAction(),
            new TransitionAction(),
            new SetAction(),
            new CommentAction(),
            new NotifyAction(),
            new SubtaskAction(),
            new EscalateAction(),
            new PostFunctionAction(),
            new WebIntegration.WebhookAction(),
            new WebApproval.ApprovalAction(),
            new ArchiveAction()
        ];

        /// <summary>
        /// Resolves an account by user name, e-mail address or display name.
        /// </summary>
        /// <param name="text">What the script names.</param>
        /// <returns>The active account, or <see langword="null"/>.</returns>
        internal static Identity ResolveIdentity(string text)
        {
            var value = text?.Trim();

            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            var identities = CoreHub.IdentityManager
                .GetIdentities(new Query<Identity>())
                .Where(x => x.State == IdentityState.Active)
                .ToList();

            return identities.FirstOrDefault(x => string.Equals(x.UserName, value, StringComparison.OrdinalIgnoreCase))
                ?? identities.FirstOrDefault(x => string.Equals(x.Email, value, StringComparison.OrdinalIgnoreCase))
                ?? identities.FirstOrDefault(x => string.Equals(x.Name, value, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns the active fields of a class.
        /// </summary>
        /// <param name="classId">The class.</param>
        /// <returns>The fields.</returns>
        internal static List<Field> FieldsOf(Guid classId)
        {
            return [.. CoreHub.FieldManager
                .GetFields(new ClassIdParameter(classId))
                .Where(x => !x.Deprecated && x.State == FieldState.Active)];
        }

        /// <summary>
        /// Writes the value of a field inside a commit attributed to the rule.
        /// </summary>
        /// <param name="context">The action context.</param>
        /// <param name="field">The field.</param>
        /// <param name="data">The value in its storage form.</param>
        internal static void WriteValue(AutomationActionContext context, Field field, string data)
        {
            var existing = CoreHub.ValueManager.GetValue(context.Object.Id, field.Id);

            // setting what is already there is no change, and no commit nobody can read anything
            // into
            if (existing is not null && string.Equals(existing.Data, data, StringComparison.Ordinal))
            {
                return;
            }

            using var commit = CoreHub.CommitManager.BeginCommit(context.Object.Id, CommitType.Updated, context.ActorId);

            if (existing is null)
            {
                CoreHub.ValueManager.Add(new Value
                {
                    ObjectId = context.Object.Id,
                    FieldId = field.Id,
                    Data = data,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                });

                return;
            }

            existing.Data = data;
            existing.Updated = DateTime.UtcNow;
            CoreHub.ValueManager.Update(existing);
        }

        /// <summary>
        /// Resolves the recipients a script names: <c>assignee</c>, <c>reporter</c>, <c>watchers</c>
        /// or accounts, comma-separated.
        /// </summary>
        /// <param name="object">The object the notification is about.</param>
        /// <param name="recipients">The recipients as written.</param>
        /// <returns>The accounts addressed.</returns>
        internal static IReadOnlyCollection<Guid> ResolveRecipients(ObjectEntity @object, string recipients)
        {
            var addressed = new HashSet<Guid>();

            foreach (var item in (recipients ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                switch (item.ToLowerInvariant())
                {
                    case "assignee":
                        if (@object?.AssigneeId is Guid assignee) { addressed.Add(assignee); }
                        break;

                    case "reporter":
                        if (@object?.CreatorId is Guid creator) { addressed.Add(creator); }
                        break;

                    case "watchers":
                        foreach (var watcher in CoreHub.WatcherManager.GetWatchers(@object.Id)) { addressed.Add(watcher.IdentityId); }
                        break;

                    default:
                        if (ResolveIdentity(item) is { } identity) { addressed.Add(identity.Id); }
                        break;
                }
            }

            addressed.Remove(Guid.Empty);

            return addressed;
        }

        /// <summary>
        /// Validates a recipient list.
        /// </summary>
        /// <param name="recipients">The recipients as written.</param>
        /// <returns><see langword="null"/> when every recipient resolves, else the problem.</returns>
        internal static string ValidateRecipients(string recipients)
        {
            var items = (recipients ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (items.Length == 0)
            {
                return "name the recipients: assignee, reporter, watchers or accounts.";
            }

            var unknown = items
                .Where(x => x.ToLowerInvariant() is not ("assignee" or "reporter" or "watchers") && ResolveIdentity(x) is null)
                .ToList();

            return unknown.Count == 0 ? null : $"unknown recipient '{unknown[0]}'.";
        }

        /// <summary>
        /// Splits <c>head: text</c> at its first colon.
        /// </summary>
        /// <param name="argument">The argument.</param>
        /// <returns>The part before the colon and the text after it.</returns>
        internal static (string Head, string Text) SplitAtColon(string argument)
        {
            var colon = (argument ?? string.Empty).IndexOf(':');

            return colon < 0
                ? (argument?.Trim() ?? string.Empty, string.Empty)
                : (argument[..colon].Trim(), argument[(colon + 1)..].Trim());
        }
    }

    /// <summary>
    /// <c>assign &lt;account | reporter | unassigned&gt;</c> - hands the object to somebody.
    /// </summary>
    internal sealed class AssignAction : IAutomationAction
    {
        /// <inheritdoc/>
        public string Key => "assign";

        /// <inheritdoc/>
        public string Syntax => "kleenestar.core:automation.action.assign";

        /// <inheritdoc/>
        public string Validate(string argument, Class @class)
        {
            return argument?.Trim().ToLowerInvariant() is "reporter" or "unassigned" || StandardActions.ResolveIdentity(argument) is not null
                ? null
                : $"unknown account '{argument}'.";
        }

        /// <inheritdoc/>
        public void Execute(AutomationActionContext context, string argument)
        {
            Guid? assignee = argument.Trim().ToLowerInvariant() switch
            {
                "unassigned" => null,
                "reporter" => context.Object.CreatorId,
                _ => StandardActions.ResolveIdentity(argument)?.Id
                    ?? throw new InvalidOperationException($"The account '{argument}' is unknown or locked.")
            };

            if (context.Object.AssigneeId == assignee)
            {
                return;
            }

            context.Object.AssigneeId = assignee;
            context.Object.Updated = DateTime.UtcNow;

            if (context.ActorId != Guid.Empty)
            {
                context.Object.UpdaterId = context.ActorId;
            }

            using var commit = CoreHub.CommitManager.BeginCommit(context.Object.Id, CommitType.Updated, context.ActorId);

            CoreHub.ObjectManager.Update(context.Object);
        }
    }

    /// <summary>
    /// <c>transition &lt;state&gt;</c> - moves the object along its workflow, through the same
    /// guards, validators and post functions a person's move goes through.
    /// </summary>
    internal sealed class TransitionAction : IAutomationAction
    {
        /// <inheritdoc/>
        public string Key => "transition";

        /// <inheritdoc/>
        public string Syntax => "kleenestar.core:automation.action.transition";

        /// <inheritdoc/>
        public string Validate(string argument, Class @class)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                return "name the state to move to.";
            }

            if (@class is null)
            {
                return null;
            }

            return CoreHub.StatusManager.GetStatuses(new ClassIdParameter(@class.Id)).Any(x => string.Equals(x.Name, argument.Trim(), StringComparison.OrdinalIgnoreCase))
                ? null
                : $"the class has no state '{argument}'.";
        }

        /// <inheritdoc/>
        public void Execute(AutomationActionContext context, string argument)
        {
            var field = StandardActions.FieldsOf(context.Object.ClassId).FirstOrDefault(x => x.FieldType == FieldType.Workflow)
                ?? throw new InvalidOperationException("The class has no workflow field.");
            var target = CoreHub.StatusManager
                .GetStatuses(new ClassIdParameter(context.Object.ClassId))
                .FirstOrDefault(x => string.Equals(x.Name, argument.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"The class has no state '{argument}'.");

            var current = CoreHub.ValueManager.GetValue(context.Object.Id, field.Id)?.Data;

            // already there - by an earlier attempt of this job, or because the object got there
            // on its own - is the outcome the rule wants
            if (string.Equals(current, target.Name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(current, target.Id.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var result = CoreHub.WorkflowManager.ExecuteTransition(context.Object.Id, field.Id, target.Id, context.ActorId);

            if (result?.Outcome != WorkflowTransitionOutcome.Executed)
            {
                throw new InvalidOperationException($"The move to '{target.Name}' was refused: {result?.Outcome} {result?.Message}".Trim());
            }
        }
    }

    /// <summary>
    /// <c>set &lt;field&gt;=&lt;value&gt;</c> - writes the value of a field.
    /// </summary>
    internal sealed class SetAction : IAutomationAction
    {
        /// <inheritdoc/>
        public string Key => "set";

        /// <inheritdoc/>
        public string Syntax => "kleenestar.core:automation.action.set";

        /// <inheritdoc/>
        public string Validate(string argument, Class @class)
        {
            var separator = (argument ?? string.Empty).IndexOf('=');

            if (separator <= 0)
            {
                return "write the field and the value as Field=Value.";
            }

            var name = argument[..separator].Trim();

            return @class is null || StandardActions.FieldsOf(@class.Id).Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
                ? null
                : $"the class has no field '{name}'.";
        }

        /// <inheritdoc/>
        public void Execute(AutomationActionContext context, string argument)
        {
            var separator = argument.IndexOf('=');
            var name = argument[..separator].Trim();
            var value = argument[(separator + 1)..].Trim();
            var field = StandardActions.FieldsOf(context.Object.ClassId).FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"The class has no field '{name}'.");

            StandardActions.WriteValue(context, field, value);
        }
    }

    /// <summary>
    /// <c>comment [internal:] &lt;text&gt;</c> - writes a comment in the rule's name; an internal
    /// one reaches the service team only.
    /// </summary>
    internal sealed class CommentAction : IAutomationAction
    {
        /// <inheritdoc/>
        public string Key => "comment";

        /// <inheritdoc/>
        public string Syntax => "kleenestar.core:automation.action.comment";

        /// <inheritdoc/>
        public string Validate(string argument, Class @class)
        {
            return string.IsNullOrWhiteSpace(Parse(argument).Text) ? "write the text of the comment." : null;
        }

        /// <inheritdoc/>
        public void Execute(AutomationActionContext context, string argument)
        {
            if (context.ActorId == Guid.Empty)
            {
                throw new InvalidOperationException("A comment needs an author: give the rule an identity to act as.");
            }

            var (visibility, text) = Parse(argument);

            // a resumed job does not write the same comment twice
            if (CoreHub.CommentManager.GetComments(context.Object.Id).Any(x => x.AuthorId == context.ActorId
                && x.Created >= context.Job.Created
                && string.Equals(x.Content, text, StringComparison.Ordinal)))
            {
                return;
            }

            CoreHub.CommentManager.Add(new Comment
            {
                ObjectId = context.Object.Id,
                AuthorId = context.ActorId,
                Content = text,
                State = CommentState.Active,
                Visibility = visibility,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Reads the audience prefix.
        /// </summary>
        /// <param name="argument">The argument.</param>
        /// <returns>The audience and the text.</returns>
        private static (CommentVisibility Visibility, string Text) Parse(string argument)
        {
            var value = argument?.Trim() ?? string.Empty;

            return value.StartsWith("internal:", StringComparison.OrdinalIgnoreCase)
                ? (CommentVisibility.InternalTeam, value["internal:".Length..].Trim())
                : (CommentVisibility.Public, value);
        }
    }

    /// <summary>
    /// <c>notify &lt;recipients&gt;: &lt;text&gt;</c> - puts a notification into the notification
    /// center of each recipient, linking the object.
    /// </summary>
    internal sealed class NotifyAction : IAutomationAction
    {
        /// <inheritdoc/>
        public string Key => "notify";

        /// <inheritdoc/>
        public string Syntax => "kleenestar.core:automation.action.notify";

        /// <inheritdoc/>
        public string Validate(string argument, Class @class)
        {
            var (recipients, text) = StandardActions.SplitAtColon(argument);

            return string.IsNullOrWhiteSpace(text)
                ? "write the recipients and the text as recipients: text."
                : StandardActions.ValidateRecipients(recipients);
        }

        /// <inheritdoc/>
        public void Execute(AutomationActionContext context, string argument)
        {
            var (recipients, text) = StandardActions.SplitAtColon(argument);
            var subject = NotificationSubject.Describe(context.Object);

            foreach (var recipient in StandardActions.ResolveRecipients(context.Object, recipients))
            {
                // the notification center addresses the identity acting on the call chain
                using var addressed = CoreHub.SessionManager.BeginIdentity(recipient);

                CoreHub.NotificationCenterManager?.Record("kleenestar.core:automation.notification.title", text, subject?.Label, subject?.TargetUri, subject?.IconUri);
            }
        }
    }

    /// <summary>
    /// <c>subtask &lt;workspace key&gt;/&lt;class&gt;: &lt;summary&gt;</c> - files a sub-task in the
    /// named class, in any workspace, joined to the object by the aggregating relation.
    /// </summary>
    internal sealed class SubtaskAction : IAutomationAction
    {
        /// <inheritdoc/>
        public string Key => "subtask";

        /// <inheritdoc/>
        public string Syntax => "kleenestar.core:automation.action.subtask";

        /// <inheritdoc/>
        public string Validate(string argument, Class @class)
        {
            var (target, summary) = StandardActions.SplitAtColon(argument);

            if (string.IsNullOrWhiteSpace(summary))
            {
                return "write the class and the summary as WORKSPACE/Class: summary.";
            }

            return ResolveClass(target) is null ? $"unknown class '{target}'." : null;
        }

        /// <inheritdoc/>
        public void Execute(AutomationActionContext context, string argument)
        {
            var (target, summary) = StandardActions.SplitAtColon(argument);
            var @class = ResolveClass(target) ?? throw new InvalidOperationException($"Unknown class '{target}'.");

            // the creation key of this action of this job: a resumed job finds the sub-task it
            // created before instead of creating a second one
            var run = ObjectCreationRun.Claim(context.IdempotencyKey, null, context.ActorId, out _);

            if (run is null)
            {
                return;
            }

            try
            {
                var id = Guid.NewGuid();
                var child = new ObjectEntity(id)
                {
                    Key = CoreHub.ObjectManager.NextObjectKey(@class.WorkspaceId),
                    Summary = summary,
                    Icon = @class.Icon,
                    State = WorkspaceState.Active,
                    WorkspaceId = @class.WorkspaceId,
                    ClassId = @class.Id,
                    SecurityLevelId = CoreHub.SecurityLevelManager?.GetDefaultSecurityLevel(@class.Id)?.Id,
                    CreatorId = context.ActorId == Guid.Empty ? null : context.ActorId,
                    UpdaterId = context.ActorId == Guid.Empty ? null : context.ActorId,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                };

                using (CoreHub.CommitManager.BeginCommit(child.Id, CommitType.Created, context.ActorId))
                {
                    CoreHub.ObjectManager.Add(child);
                    run.TrackObject(child.Id);
                }

                var type = CoreHub.ObjectRelationTypeManager
                    .GetRelationTypes()
                    .Where(x => x.Active && x.Effect == WebExpress.WebApp.WebRelation.RelationEffect.AggregatesProgress)
                    .OrderBy(x => x.Order)
                    .FirstOrDefault();

                if (type is not null)
                {
                    var relation = new ObjectRelation
                    {
                        Id = Guid.NewGuid(),
                        System = WebExpress.WebApp.WebRelation.RelationSystem.Object,
                        TypeKey = type.Key,
                        Direction = WebExpress.WebApp.WebRelation.RelationDirection.Bidirectional,
                        Status = WebExpress.WebApp.WebRelation.RelationStatus.Active,
                        SourceObjectId = context.Object.Id,
                        TargetObjectId = child.Id,
                        CreatedById = context.ActorId == Guid.Empty ? null : context.ActorId,
                        Created = DateTime.UtcNow,
                        Updated = DateTime.UtcNow
                    };

                    CoreHub.ObjectRelationManager.Add(relation);
                    run.TrackRelation(relation.Id);
                }

                run.Complete(child.Id);
            }
            catch (Exception ex)
            {
                run.RollBack(ex);

                throw;
            }
        }

        /// <summary>
        /// Resolves <c>WORKSPACE/Class</c>.
        /// </summary>
        /// <param name="target">The class as written.</param>
        /// <returns>The class, or <see langword="null"/>.</returns>
        private static Class ResolveClass(string target)
        {
            var slash = (target ?? string.Empty).IndexOf('/');

            if (slash <= 0)
            {
                return null;
            }

            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(target[..slash].Trim());
            var name = target[(slash + 1)..].Trim();

            return workspace is null
                ? null
                : CoreHub.ClassManager
                    .GetClasses(new Query<Class>().WhereEquals(x => x.WorkspaceId, workspace.Id))
                    .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// <c>escalate [priority]</c> - raises the priority one step, or to the named priority.
    /// </summary>
    internal sealed class EscalateAction : IAutomationAction
    {
        /// <inheritdoc/>
        public string Key => "escalate";

        /// <inheritdoc/>
        public string Syntax => "kleenestar.core:automation.action.escalate";

        /// <inheritdoc/>
        public string Validate(string argument, Class @class)
        {
            if (string.IsNullOrWhiteSpace(argument) || @class is null)
            {
                return null;
            }

            return Priorities(@class.Id).Any(x => string.Equals(x.Name, argument.Trim(), StringComparison.OrdinalIgnoreCase))
                ? null
                : $"the class has no priority '{argument}'.";
        }

        /// <inheritdoc/>
        public void Execute(AutomationActionContext context, string argument)
        {
            var field = StandardActions.FieldsOf(context.Object.ClassId).FirstOrDefault(x => x.FieldType == FieldType.Priority)
                ?? throw new InvalidOperationException("The class has no priority field.");
            var priorities = Priorities(context.Object.ClassId);

            if (priorities.Count == 0)
            {
                return;
            }

            Priority target;

            if (!string.IsNullOrWhiteSpace(argument))
            {
                target = priorities.FirstOrDefault(x => string.Equals(x.Name, argument.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"The class has no priority '{argument}'.");
            }
            else
            {
                // the order runs from the most urgent down; one step up is the one before the
                // current, and the most urgent stays where it is
                var current = CoreHub.ValueManager.GetValue(context.Object.Id, field.Id)?.Data;
                var index = priorities.FindIndex(x => string.Equals(x.Name, current, StringComparison.OrdinalIgnoreCase));

                target = index < 0 ? priorities[^1] : priorities[Math.Max(0, index - 1)];
            }

            StandardActions.WriteValue(context, field, target.Name);
        }

        /// <summary>
        /// Returns the active priorities of a class, the most urgent first.
        /// </summary>
        /// <param name="classId">The class.</param>
        /// <returns>The priorities.</returns>
        private static List<Priority> Priorities(Guid classId)
        {
            return [.. CoreHub.PriorityManager
                .GetPriorities(new ClassIdParameter(classId))
                .Where(x => x.State == PriorityState.Active)
                .OrderBy(x => x.Order)];
        }
    }

    /// <summary>
    /// <c>postfunction &lt;key&gt;</c> - runs a workflow post function, so what a transition can
    /// do a rule can do as well.
    /// </summary>
    internal sealed class PostFunctionAction : IAutomationAction
    {
        /// <inheritdoc/>
        public string Key => "postfunction";

        /// <inheritdoc/>
        public string Syntax => "kleenestar.core:automation.action.postfunction";

        /// <inheritdoc/>
        public string Validate(string argument, Class @class)
        {
            return CoreHub.WorkflowPostFunctionManager?.Get(argument?.Trim()) is null
                ? $"unknown post function '{argument}'."
                : null;
        }

        /// <inheritdoc/>
        public void Execute(AutomationActionContext context, string argument)
        {
            var function = CoreHub.WorkflowPostFunctionManager?.Get(argument.Trim())
                ?? throw new InvalidOperationException($"Unknown post function '{argument}'.");

            function.Execute(new WorkflowRuleContext
            {
                Object = context.Object,
                IdentityId = context.ActorId
            });
        }
    }
}
