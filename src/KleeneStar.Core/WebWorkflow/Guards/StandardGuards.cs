using KleeneStar.Core.WebManager;
using System;
using System.Linq;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebWorkflow.Guards
{
    /// <summary>
    /// The move may only be taken while the object has somebody responsible for it.
    /// </summary>
    /// <remarks>
    /// The most common condition of all: work that is being started belongs to somebody, and a
    /// board full of unassigned items in progress is what this refuses.
    /// </remarks>
    public sealed class AssigneeSetGuard : IWorkflowGuard
    {
        /// <inheritdoc/>
        public string Key => "assignee.set";

        /// <inheritdoc/>
        public string Label => "kleenestar.core:workflow.rule.assignee.set.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:workflow.rule.assignee.set.description";

        /// <inheritdoc/>
        public IIcon Icon => new IconUser();

        /// <inheritdoc/>
        public int Order => 10;

        /// <inheritdoc/>
        public bool Evaluate(WorkflowRuleContext context)
        {
            return context?.Object?.AssigneeId is Guid assignee && assignee != Guid.Empty;
        }
    }

    /// <summary>
    /// Only the person the object is assigned to may take the move.
    /// </summary>
    /// <remarks>
    /// Not the same as <see cref="AssigneeSetGuard"/>: that one asks whether anybody owns the
    /// work, this one whether the owner is the person making the move. It is how a workflow
    /// keeps a review out of the hands of everyone except the reviewer.
    /// </remarks>
    public sealed class ActorIsAssigneeGuard : IWorkflowGuard
    {
        /// <inheritdoc/>
        public string Key => "actor.is.assignee";

        /// <inheritdoc/>
        public string Label => "kleenestar.core:workflow.rule.actor.is.assignee.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:workflow.rule.actor.is.assignee.description";

        /// <inheritdoc/>
        public IIcon Icon => new IconUserCheck();

        /// <inheritdoc/>
        public int Order => 20;

        /// <inheritdoc/>
        public bool Evaluate(WorkflowRuleContext context)
        {
            // a move nobody is signed in for is not the assignee's, whoever that is - the system
            // moving an object on its own has no claim to a person's role
            return context is not null
                && context.IdentityId != Guid.Empty
                && context.Object?.AssigneeId == context.IdentityId;
        }
    }

    /// <summary>
    /// Only the person who created the object may take the move.
    /// </summary>
    /// <remarks>
    /// The condition behind "only the reporter closes their own ticket": the person who asked for
    /// something is the one who can say it is done.
    /// </remarks>
    public sealed class ActorIsCreatorGuard : IWorkflowGuard
    {
        /// <inheritdoc/>
        public string Key => "actor.is.creator";

        /// <inheritdoc/>
        public string Label => "kleenestar.core:workflow.rule.actor.is.creator.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:workflow.rule.actor.is.creator.description";

        /// <inheritdoc/>
        public IIcon Icon => new IconUserPen();

        /// <inheritdoc/>
        public int Order => 30;

        /// <inheritdoc/>
        public bool Evaluate(WorkflowRuleContext context)
        {
            return context is not null
                && context.IdentityId != Guid.Empty
                && context.Object?.CreatorId == context.IdentityId;
        }
    }

    /// <summary>
    /// The move may only be taken while nothing that blocks the object is still open.
    /// </summary>
    /// <remarks>
    /// The relation model enforces this for a move into a closing state on its own
    /// (<see cref="ObjectRelationWorkflowRules"/>); as a guard it can be put on any move, which
    /// is how a workflow refuses to let work <em>start</em> while its predecessor is unfinished.
    /// </remarks>
    public sealed class NoOpenBlockersGuard : IWorkflowGuard
    {
        /// <inheritdoc/>
        public string Key => "relations.unblocked";

        /// <inheritdoc/>
        public string Label => "kleenestar.core:workflow.rule.relations.unblocked.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:workflow.rule.relations.unblocked.description";

        /// <inheritdoc/>
        public IIcon Icon => new IconFlag();

        /// <inheritdoc/>
        public int Order => 40;

        /// <inheritdoc/>
        public bool Evaluate(WorkflowRuleContext context)
        {
            if (context?.Object is null)
            {
                return false;
            }

            // asked of the target the move is heading for, because that is the state the
            // relation rules judge - the same question the built-in guard asks, put where an
            // administrator can put it on any transition
            return ObjectRelationWorkflowRules.FindBlockers(context.Object.Id, context.Target).Count == 0;
        }
    }

    /// <summary>
    /// The move may only be taken once everything the object aggregates is finished.
    /// </summary>
    /// <remarks>
    /// The condition a container needs: an epic cannot be done while its stories are not, and the
    /// progress rollup already knows how far they have come. An object that aggregates nothing
    /// satisfies it - there is nothing outstanding.
    /// </remarks>
    public sealed class ChildrenCompletedGuard : IWorkflowGuard
    {
        /// <inheritdoc/>
        public string Key => "children.completed";

        /// <inheritdoc/>
        public string Label => "kleenestar.core:workflow.rule.children.completed.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:workflow.rule.children.completed.description";

        /// <inheritdoc/>
        public IIcon Icon => new IconSitemap();

        /// <inheritdoc/>
        public int Order => 50;

        /// <inheritdoc/>
        public bool Evaluate(WorkflowRuleContext context)
        {
            if (context?.Object is null)
            {
                return false;
            }

            var progress = CoreHub.ObjectProgressManager?.GetProgress(context.Object.Id);

            return progress is null
                || !progress.Aggregated
                || progress.Contributions.All(x => x.Percent >= 100);
        }
    }
}
