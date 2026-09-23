using KleeneStar.Model.Entities;
using Calendar = KleeneStar.Model.Entities.Calendar;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The wiring that gives the audit log its coverage: one subscription per managed change,
    /// so recording an event is not something each manager has to remember to do.
    /// </summary>
    /// <remarks>
    /// This is the part of the design that decides whether the log is complete in practice.
    /// Sprinkling <c>Record(...)</c> calls through thirty managers would put the completeness of
    /// the audit trail at the mercy of every future edit to any of them - and a missing call is
    /// invisible, because a hole in an audit log looks exactly like a period of inactivity.
    /// Subscribing centrally to the events those managers already raise means a change reaches
    /// the log by the same path it reaches the rest of the application, and a new manager is
    /// audited by adding one line here rather than by remembering a convention.
    /// <para>
    /// Two areas are deliberately not subscribed. Field values and object mutations arrive
    /// through <see cref="ICommitManager.CommitAdded"/> instead, which carries the exact before
    /// and after of every attribute the action touched - far better than a diff reconstructed
    /// afterwards. Per-identity conveniences (notifications marked read, quickfilters, saved
    /// searches, recent visits) are not recorded at all: they change nothing anybody else can
    /// observe, and burying the events that matter under them would make the log less useful,
    /// not more complete.
    /// </para>
    /// </remarks>
    public sealed partial class AuditManager
    {
        /// <summary>
        /// Subscribes the log to the managers whose changes it records.
        /// </summary>
        /// <remarks>
        /// Called once, after the hub is populated and the database is migrated. It is
        /// idempotent because <c>Run</c> is documented as a concurrent call: subscribing twice
        /// would double every event in the log, which would not merely be noisy but would make
        /// the log disagree with itself about how often something happened.
        /// </remarks>
        public void Connect()
        {
            lock (_connectionGate)
            {
                if (_connected)
                {
                    return;
                }

                _connected = true;
            }

            ConnectContent();
            ConnectConfiguration();
            ConnectIdentity();
            ConnectSecurity();
        }

        /// <summary>
        /// Subscribes to the changes made to the data the installation holds.
        /// </summary>
        /// <remarks>
        /// Objects are audited from their commits rather than from the object manager. The
        /// commit already carries the exact before and after of every attribute the action
        /// touched and the revision number the object reached, so the audit event it produces is
        /// both more precise and cheaper than one derived by diffing afterwards - and the
        /// revision is what links the two records of the same change to each other.
        /// </remarks>
        private void ConnectContent()
        {
            CoreHub.CommitManager.CommitAdded += (_, commit) => OnCommit(commit);
            CoreHub.CommitManager.CommitRestored += (_, result) => OnRestore(result);

            CoreHub.WorkflowManager.TransitionExecuted += (_, result) => OnTransition(result);

            CoreHub.CommentManager.CommentAdded += (_, x) => RecordChange(AuditCategory.Content, AuditAction.Created, x);
            CoreHub.CommentManager.CommentUpdated += (_, x) => RecordChange(AuditCategory.Content, AuditAction.Updated, x);
            CoreHub.CommentManager.CommentRemoved += (_, x) => RecordChange(AuditCategory.Content, AuditAction.Deleted, x, AuditSeverity.Notice);

            CoreHub.AttachmentManager.AttachmentAdded += (_, x) => RecordChange(AuditCategory.Content, AuditAction.Created, x);
            CoreHub.AttachmentManager.AttachmentRemoved += (_, x) => RecordChange(AuditCategory.Content, AuditAction.Deleted, x, AuditSeverity.Notice);

            CoreHub.ObjectRelationManager.RelationAdded += (_, x) => RecordChange(AuditCategory.Content, AuditAction.Created, x);
            CoreHub.ObjectRelationManager.RelationUpdated += (_, x) => RecordChange(AuditCategory.Content, AuditAction.Updated, x);
            CoreHub.ObjectRelationManager.RelationRemoved += (_, x) => RecordChange(AuditCategory.Content, AuditAction.Deleted, x);

            CoreHub.ObjectTagManager.TagAdded += (_, x) => RecordChange(AuditCategory.Content, AuditAction.Created, x);
            CoreHub.ObjectTagManager.TagRemoved += (_, x) => RecordChange(AuditCategory.Content, AuditAction.Deleted, x);

            // a share hands a record to somebody who could not otherwise see it, which is an
            // access decision rather than an edit
            CoreHub.ShareManager.ShareAdded += (_, x) => RecordChange(AuditCategory.Authorization, AuditAction.PermissionGranted, x, AuditSeverity.Notice);
            CoreHub.ShareManager.ShareRemoved += (_, x) => RecordChange(AuditCategory.Authorization, AuditAction.PermissionRevoked, x, AuditSeverity.Notice);

            CoreHub.SprintManager.SprintAdded += (_, x) => RecordChange(AuditCategory.Content, AuditAction.Created, x);
            CoreHub.SprintManager.SprintUpdated += (_, x) => RecordChange(AuditCategory.Content, AuditAction.Updated, x);
            CoreHub.SprintManager.SprintRemoved += (_, x) => RecordChange(AuditCategory.Content, AuditAction.Deleted, x, AuditSeverity.Notice);
        }

        /// <summary>
        /// Subscribes to the changes made to the shape of the installation.
        /// </summary>
        /// <remarks>
        /// Everything here is recorded at <see cref="AuditSeverity.Notice"/> at least, and
        /// deletions higher. A configuration change is never local: removing a field removes
        /// what every object of its class held in it, and changing a workflow changes what every
        /// object of every class using it may do next. Those are the changes a later
        /// investigation of "why did this object behave like that" has to be able to find.
        /// </remarks>
        private void ConnectConfiguration()
        {
            Configuration<Workspace>(h => CoreHub.WorkspaceManager.WorkspaceAdded += h, h => CoreHub.WorkspaceManager.WorkspaceUpdated += h, h => CoreHub.WorkspaceManager.WorkspaceRemoved += h);
            Configuration<Class>(h => CoreHub.ClassManager.ClassAdded += h, h => CoreHub.ClassManager.ClassUpdated += h, h => CoreHub.ClassManager.ClassRemoved += h);
            Configuration<Field>(h => CoreHub.FieldManager.FieldAdded += h, h => CoreHub.FieldManager.FieldUpdated += h, h => CoreHub.FieldManager.FieldRemoved += h);
            Configuration<SecurityLevel>(h => CoreHub.SecurityLevelManager.SecurityLevelAdded += h, h => CoreHub.SecurityLevelManager.SecurityLevelUpdated += h, h => CoreHub.SecurityLevelManager.SecurityLevelRemoved += h);
            Configuration<Form>(h => CoreHub.FormManager.FormAdded += h, h => CoreHub.FormManager.FormUpdated += h, h => CoreHub.FormManager.FormRemoved += h);
            Configuration<Template>(h => CoreHub.TemplateManager.TemplateAdded += h, h => CoreHub.TemplateManager.TemplateUpdated += h, h => CoreHub.TemplateManager.TemplateRemoved += h);
            Configuration<Priority>(h => CoreHub.PriorityManager.PriorityAdded += h, h => CoreHub.PriorityManager.PriorityUpdated += h, h => CoreHub.PriorityManager.PriorityRemoved += h);
            Configuration<Workflow>(h => CoreHub.WorkflowManager.WorkflowAdded += h, h => CoreHub.WorkflowManager.WorkflowUpdated += h, h => CoreHub.WorkflowManager.WorkflowRemoved += h);
            Configuration<Status>(h => CoreHub.StatusManager.StatusAdded += h, h => CoreHub.StatusManager.StatusUpdated += h, h => CoreHub.StatusManager.StatusRemoved += h);
            Configuration<SlaPolicy>(h => CoreHub.SlaManager.SlaAdded += h, h => CoreHub.SlaManager.SlaUpdated += h, h => CoreHub.SlaManager.SlaRemoved += h);
            Configuration<Calendar>(h => CoreHub.CalendarManager.CalendarAdded += h, h => CoreHub.CalendarManager.CalendarUpdated += h, h => CoreHub.CalendarManager.CalendarRemoved += h);
            Configuration<Dashboard>(h => CoreHub.DashboardManager.DashboardAdded += h, h => CoreHub.DashboardManager.DashboardUpdated += h, h => CoreHub.DashboardManager.DashboardRemoved += h);
            Configuration<ObjectView>(h => CoreHub.ObjectViewManager.ObjectViewAdded += h, h => CoreHub.ObjectViewManager.ObjectViewUpdated += h, h => CoreHub.ObjectViewManager.ObjectViewRemoved += h);
            Configuration<NavigatorLink>(h => CoreHub.NavigatorLinkManager.NavigatorLinkAdded += h, h => CoreHub.NavigatorLinkManager.NavigatorLinkUpdated += h, h => CoreHub.NavigatorLinkManager.NavigatorLinkRemoved += h);
            Configuration<ObjectRelationType>(h => CoreHub.ObjectRelationTypeManager.RelationTypeAdded += h, h => CoreHub.ObjectRelationTypeManager.RelationTypeUpdated += h, h => CoreHub.ObjectRelationTypeManager.RelationTypeRemoved += h);

            CoreHub.BrandingManager.BrandingUpdated += (_, x) => RecordChange(AuditCategory.Configuration, AuditAction.Updated, x, AuditSeverity.Notice);
            CoreHub.MaintenanceManager.MaintenanceUpdated += (_, x) => RecordChange(AuditCategory.Configuration, AuditAction.Updated, x, AuditSeverity.Notice);
        }

        /// <summary>
        /// Subscribes to the changes made to who exists and who they belong to.
        /// </summary>
        private void ConnectIdentity()
        {
            CoreHub.IdentityManager.IdentityAdded += (_, x) => RecordChange(AuditCategory.Identity, AuditAction.Created, x, AuditSeverity.Notice);
            CoreHub.IdentityManager.IdentityUpdated += (_, x) => RecordChange(AuditCategory.Identity, AuditAction.Updated, x, AuditSeverity.Notice);
            CoreHub.IdentityManager.IdentityRemoved += (_, x) => RecordChange(AuditCategory.Identity, AuditAction.Deleted, x, AuditSeverity.Critical);

            CoreHub.GroupManager.GroupAdded += (_, x) => RecordChange(AuditCategory.Identity, AuditAction.Created, x, AuditSeverity.Notice);
            CoreHub.GroupManager.GroupUpdated += (_, x) => RecordChange(AuditCategory.Identity, AuditAction.Updated, x, AuditSeverity.Notice);
            CoreHub.GroupManager.GroupRemoved += (_, x) => RecordChange(AuditCategory.Identity, AuditAction.Deleted, x, AuditSeverity.Critical);

            CoreHub.TenantManager.TenantAdded += (_, x) => RecordChange(AuditCategory.Identity, AuditAction.Created, x, AuditSeverity.Notice);
            CoreHub.TenantManager.TenantUpdated += (_, x) => RecordChange(AuditCategory.Identity, AuditAction.Updated, x, AuditSeverity.Notice);
            CoreHub.TenantManager.TenantRemoved += (_, x) => RecordChange(AuditCategory.Identity, AuditAction.Deleted, x, AuditSeverity.Critical);
        }

        /// <summary>
        /// Subscribes to the changes made to credentials and to who may do what.
        /// </summary>
        /// <remarks>
        /// A permission grant is recorded as <see cref="AuditSeverity.Critical"/> rather than
        /// merely noticed. It is the change that makes every subsequent action by the granted
        /// group legitimate, so a reader reconstructing why somebody was able to do something
        /// has to be able to find it without knowing what to look for.
        /// </remarks>
        private void ConnectSecurity()
        {
            CoreHub.PermissionManager.PermissionAssigned += (_, x) => RecordChange(AuditCategory.Authorization, AuditAction.PermissionGranted, x, AuditSeverity.Critical);
            CoreHub.PermissionManager.PermissionRevoked += (_, x) => RecordChange(AuditCategory.Authorization, AuditAction.PermissionRevoked, x, AuditSeverity.Critical);

            CoreHub.AccessTokenManager.AccessTokenAdded += (_, x) => RecordChange(AuditCategory.Security, AuditAction.TokenIssued, x, AuditSeverity.Notice);
            CoreHub.AccessTokenManager.AccessTokenUpdated += (_, x) => RecordChange(AuditCategory.Security, AuditAction.Updated, x, AuditSeverity.Notice);
            CoreHub.AccessTokenManager.AccessTokenRemoved += (_, x) => RecordChange(AuditCategory.Security, AuditAction.TokenRevoked, x, AuditSeverity.Notice);

            CoreHub.IdentitySessionManager.IdentitySessionRemoved += (_, x) => RecordChange(AuditCategory.Security, AuditAction.SessionRevoked, x, AuditSeverity.Notice);

            // the password itself reaches the log in no form; the actor says who set it - the
            // owner, or nobody when a reset link did - and the target says whose it is
            CoreHub.CredentialManager.PasswordChanged += (_, x) => Record
            (
                AuditCategory.Security,
                AuditAction.PasswordChanged,
                new AuditTarget(AuditTargetType.Identity, x.Id, x.UserName ?? x.Name),
                [],
                AuditOutcome.Succeeded,
                AuditSeverity.Notice
            );
            CoreHub.CredentialManager.PasswordResetIssued += (_, x) => Record
            (
                AuditCategory.Security,
                AuditAction.PasswordResetIssued,
                new AuditTarget(AuditTargetType.Identity, x.IdentityId),
                [AuditDelta.Added("expires", x.Expires.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), AuditValueKind.Timestamp)],
                AuditOutcome.Succeeded,
                AuditSeverity.Notice
            );
        }

        /// <summary>
        /// Subscribes the three lifecycle events of one configuration entity in one line, so the
        /// list above reads as the inventory of what is audited rather than as a wall of
        /// handlers.
        /// </summary>
        /// <typeparam name="TEntity">The entity type the manager raises.</typeparam>
        /// <param name="added">Attaches the handler to the manager's added event.</param>
        /// <param name="updated">Attaches the handler to the manager's updated event.</param>
        /// <param name="removed">Attaches the handler to the manager's removed event.</param>
        private void Configuration<TEntity>(Action<EventHandler<TEntity>> added, Action<EventHandler<TEntity>> updated, Action<EventHandler<TEntity>> removed)
        {
            added((_, x) => RecordChange(AuditCategory.Configuration, AuditAction.Created, x, AuditSeverity.Notice));
            updated((_, x) => RecordChange(AuditCategory.Configuration, AuditAction.Updated, x, AuditSeverity.Notice));
            removed((_, x) => RecordChange(AuditCategory.Configuration, AuditAction.Deleted, x, AuditSeverity.Critical));
        }

        /// <summary>
        /// Records the audit event a commit produced.
        /// </summary>
        /// <remarks>
        /// The deltas are translated from the commit's own changes rather than derived, so the
        /// audit event states exactly what the versioning store stated - including the
        /// distinction between an attribute that was cleared and one that was never set, which
        /// the commit encodes as a change to a null value and the log encodes as a removal.
        /// <see cref="AuditEvent.TargetRevision"/> carries the commit number, which is what lets
        /// an audit entry be opened as a revision of the object it describes.
        /// </remarks>
        /// <param name="commit">The commit that was appended.</param>
        private void OnCommit(Commit commit)
        {
            if (commit is null)
            {
                return;
            }

            var target = new AuditTarget(AuditTargetType.Object, commit.ObjectId, commit.ObjectKey, commit.Number);

            var action = commit.Type switch
            {
                CommitType.Created => AuditAction.Created,
                CommitType.Transitioned => AuditAction.Transitioned,
                CommitType.Archived => AuditAction.Archived,
                CommitType.Restored => AuditAction.Restored,
                CommitType.Deleted => AuditAction.Deleted,
                _ => AuditAction.Updated
            };

            var severity = commit.Type == CommitType.Deleted
                ? AuditSeverity.Notice
                : AuditSeverity.Info;

            Record(AuditCategory.Content, action, target, Translate(commit), AuditOutcome.Succeeded, severity);
        }

        /// <summary>
        /// Records that a historical state of an object was reapplied.
        /// </summary>
        /// <param name="result">The outcome of the restore.</param>
        private void OnRestore(CommitRestoreResult result)
        {
            if (result is null || !result.Changed)
            {
                return;
            }

            // the commit the restore appended is audited by OnCommit; this event records the
            // decision behind it, which the commit itself does not carry
            var target = new AuditTarget(AuditTargetType.Object, result.ObjectId, result.ObjectKey, result.Commit?.Number);

            Record
            (
                AuditCategory.Content,
                AuditAction.Restored,
                target,
                [AuditDelta.Added("restoredfrom", result.RestoredNumber.ToString(CultureInfo.InvariantCulture), AuditValueKind.Number)],
                AuditOutcome.Succeeded,
                AuditSeverity.Notice
            );
        }

        /// <summary>
        /// Records a workflow transition, whether or not it was allowed to happen.
        /// </summary>
        /// <remarks>
        /// A refused transition is recorded as well as an executed one. A workflow exists to
        /// make certain moves impossible, so the attempts it stopped are evidence that it did
        /// its job - and, in the other reading, evidence of somebody repeatedly trying to make a
        /// move they should not be making.
        /// </remarks>
        /// <param name="result">The outcome of the transition.</param>
        private void OnTransition(WorkflowTransitionResult result)
        {
            if (result is null)
            {
                return;
            }

            var deltas = new List<AuditDelta>
            {
                AuditDelta.Modified
                (
                    "status",
                    result.Source?.Name,
                    result.Target?.Name,
                    AuditValueKind.Text,
                    result.FieldId == Guid.Empty ? null : result.FieldId
                )
            };

            if (result.Transition is not null)
            {
                deltas.Add(AuditDelta.Added("transition", result.Transition.Name, AuditValueKind.Text));
            }

            foreach (var error in result.ValidationErrors ?? [])
            {
                deltas.Add(AuditDelta.Added("validation", error, AuditValueKind.Text));
            }

            Record
            (
                AuditCategory.Workflow,
                AuditAction.Transitioned,
                new AuditTarget(AuditTargetType.Object, result.ObjectId, null),
                deltas,
                result.Succeeded ? AuditOutcome.Succeeded : AuditOutcome.Denied,
                result.Succeeded ? AuditSeverity.Info : AuditSeverity.Warning
            );
        }

        /// <summary>
        /// Translates the changes of a commit into audit deltas.
        /// </summary>
        /// <remarks>
        /// The kind is decided from the payloads here, which is the one place it legitimately
        /// can be: a <see cref="Change"/> row records exactly what an attribute held before and
        /// after, so an absent old value means the attribute was not there and an absent new
        /// value means it no longer is. Everywhere else in the log the kind is stated by the
        /// caller, because everywhere else that inference would be a guess.
        /// </remarks>
        /// <param name="commit">The commit.</param>
        /// <returns>The deltas, in the order the changes were recorded.</returns>
        private static IReadOnlyList<AuditDelta> Translate(Commit commit)
        {
            return [.. (commit.Changes ?? [])
                .OrderBy(x => x.Ordinal)
                .Select(change =>
                {
                    var hadValue = !string.IsNullOrEmpty(change.OldValue);
                    var hasValue = !string.IsNullOrEmpty(change.NewValue);

                    if (!hadValue && hasValue)
                    {
                        return AuditDelta.Added(change.Name, change.NewValue, AuditValueKind.Text, change.FieldId);
                    }

                    if (hadValue && !hasValue)
                    {
                        return AuditDelta.Removed(change.Name, change.OldValue, AuditValueKind.Text, change.FieldId);
                    }

                    return AuditDelta.Modified(change.Name, change.OldValue, change.NewValue, AuditValueKind.Text, change.FieldId);
                })];
        }
    }
}
