using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebWorkflow.Validators;
using KleeneStar.Model.Entities;
using System;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebWorkflow.PostFunctions
{
    /// <summary>
    /// Assigns the object to whoever made the move.
    /// </summary>
    /// <remarks>
    /// The action behind "start work": taking a piece of work is the same gesture as saying it is
    /// yours, and a workflow that makes somebody do both by hand ends up with a board of started
    /// items nobody owns. A move nobody is signed in for assigns nothing rather than clearing the
    /// assignee.
    /// </remarks>
    public sealed class AssignToActorPostFunction : IWorkflowPostFunction
    {
        /// <inheritdoc/>
        public string Key => "assign.to.actor";

        /// <inheritdoc/>
        public string Label => "kleenestar.core:workflow.rule.assign.to.actor.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:workflow.rule.assign.to.actor.description";

        /// <inheritdoc/>
        public IIcon Icon => new IconUserCheck();

        /// <inheritdoc/>
        public int Order => 10;

        /// <inheritdoc/>
        public void Execute(WorkflowRuleContext context)
        {
            if (context?.Object is null || context.IdentityId == Guid.Empty)
            {
                return;
            }

            context.Object.AssigneeId = context.IdentityId;

            CoreHub.ObjectManager.Update(context.Object);
        }
    }

    /// <summary>
    /// Clears whoever the object was assigned to.
    /// </summary>
    /// <remarks>
    /// The counterpart of taking work: an item put back on the board belongs to nobody until
    /// somebody picks it up, and leaving the previous owner on it makes a queue look staffed.
    /// </remarks>
    public sealed class ClearAssigneePostFunction : IWorkflowPostFunction
    {
        /// <inheritdoc/>
        public string Key => "assignee.clear";

        /// <inheritdoc/>
        public string Label => "kleenestar.core:workflow.rule.assignee.clear.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:workflow.rule.assignee.clear.description";

        /// <inheritdoc/>
        public IIcon Icon => new IconUserMinus();

        /// <inheritdoc/>
        public int Order => 20;

        /// <inheritdoc/>
        public void Execute(WorkflowRuleContext context)
        {
            if (context?.Object is null)
            {
                return;
            }

            context.Object.AssigneeId = null;

            CoreHub.ObjectManager.Update(context.Object);
        }
    }

    /// <summary>
    /// Writes what was typed on the screen of the transition into the discussion of the object.
    /// </summary>
    /// <remarks>
    /// A note given while moving an object is a statement about the object, not about the
    /// transition, and the place people look for statements is the comment thread. Without this
    /// the text would live only in the commit of the state change, where nobody reads it.
    /// <para>
    /// Nothing is written when the screen carried no note, and nothing when the move was made by
    /// the system: a comment needs an author, and the discussion is a record of what people said.
    /// </para>
    /// </remarks>
    public sealed class ScreenNoteAsCommentPostFunction : IWorkflowPostFunction
    {
        /// <inheritdoc/>
        public string Key => "screen.note.comment";

        /// <inheritdoc/>
        public string Label => "kleenestar.core:workflow.rule.screen.note.comment.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:workflow.rule.screen.note.comment.description";

        /// <inheritdoc/>
        public IIcon Icon => new IconComment();

        /// <inheritdoc/>
        public int Order => 30;

        /// <inheritdoc/>
        public void Execute(WorkflowRuleContext context)
        {
            var note = context is null ? null : ScreenNoteGivenValidator.ReadNote(context);

            if (context?.Object is null || string.IsNullOrWhiteSpace(note) || context.IdentityId == Guid.Empty)
            {
                return;
            }

            CoreHub.CommentManager.Add(new Comment
            {
                Id = Guid.NewGuid(),
                ObjectId = context.Object.Id,
                AuthorId = context.IdentityId,
                Content = note.Trim(),
                State = CommentState.Active,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Makes whoever moved the object a watcher of it.
    /// </summary>
    /// <remarks>
    /// Somebody who acts on an object has an interest in what happens to it next, and the usual
    /// alternative - remembering to press <em>watch</em> - is what makes people miss the answer
    /// to their own question. Watching twice is not an error: the manager keeps one row per
    /// identity.
    /// </remarks>
    public sealed class WatchActorPostFunction : IWorkflowPostFunction
    {
        /// <inheritdoc/>
        public string Key => "actor.watch";

        /// <inheritdoc/>
        public string Label => "kleenestar.core:workflow.rule.actor.watch.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:workflow.rule.actor.watch.description";

        /// <inheritdoc/>
        public IIcon Icon => new IconEye();

        /// <inheritdoc/>
        public int Order => 40;

        /// <inheritdoc/>
        public void Execute(WorkflowRuleContext context)
        {
            if (context?.Object is null || context.IdentityId == Guid.Empty)
            {
                return;
            }

            CoreHub.WatcherManager.Add(context.Object.Id, context.IdentityId);
        }
    }
}
