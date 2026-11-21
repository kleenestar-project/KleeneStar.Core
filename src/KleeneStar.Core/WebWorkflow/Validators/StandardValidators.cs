using KleeneStar.Core.WebManager;
using System;
using System.Linq;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebWorkflow.Validators
{
    /// <summary>
    /// The object has to carry a summary.
    /// </summary>
    /// <remarks>
    /// Every object carries the field whatever its class models, which is what makes this one of
    /// the few validators that can be shipped: it holds for a class nobody has configured yet.
    /// </remarks>
    public sealed class SummarySetValidator : IWorkflowValidator
    {
        /// <inheritdoc/>
        public string Key => "summary.set";

        /// <inheritdoc/>
        public string Label => "kleenestar.core:workflow.rule.summary.set.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:workflow.rule.summary.set.description";

        /// <inheritdoc/>
        public string Message => "kleenestar.core:workflow.rule.summary.set.message";

        /// <inheritdoc/>
        public IIcon Icon => new IconHeading();

        /// <inheritdoc/>
        public int Order => 10;

        /// <inheritdoc/>
        public bool Evaluate(WorkflowRuleContext context)
        {
            return !string.IsNullOrWhiteSpace(context?.Object?.Summary);
        }
    }

    /// <summary>
    /// The object has to carry a description.
    /// </summary>
    /// <remarks>
    /// What a workflow demands before work starts: an item nobody described is one nobody can
    /// pick up. The check is on the stored description rather than on the screen, so it holds
    /// whether the text was written on the object or in the transition.
    /// </remarks>
    public sealed class DescriptionSetValidator : IWorkflowValidator
    {
        /// <inheritdoc/>
        public string Key => "description.set";

        /// <inheritdoc/>
        public string Label => "kleenestar.core:workflow.rule.description.set.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:workflow.rule.description.set.description";

        /// <inheritdoc/>
        public string Message => "kleenestar.core:workflow.rule.description.set.message";

        /// <inheritdoc/>
        public IIcon Icon => new IconAlignLeft();

        /// <inheritdoc/>
        public int Order => 20;

        /// <inheritdoc/>
        public bool Evaluate(WorkflowRuleContext context)
        {
            return !string.IsNullOrWhiteSpace(context?.Object?.Description);
        }
    }

    /// <summary>
    /// The object has to be assigned to somebody.
    /// </summary>
    /// <remarks>
    /// The same question <c>assignee.set</c> guards with, asked at the other end: as a guard it
    /// hides the move, as a validator it offers the move and says what is missing. Which of the
    /// two an installation wants is a matter of how much its people are expected to know about
    /// the workflow, and both readings are legitimate - which is why the same condition is
    /// registered in both catalogs rather than only in one.
    /// </remarks>
    public sealed class AssigneeSetValidator : IWorkflowValidator
    {
        /// <inheritdoc/>
        public string Key => "assignee.set";

        /// <inheritdoc/>
        public string Label => "kleenestar.core:workflow.rule.assignee.set.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:workflow.rule.assignee.set.description";

        /// <inheritdoc/>
        public string Message => "kleenestar.core:workflow.rule.assignee.set.message";

        /// <inheritdoc/>
        public IIcon Icon => new IconUser();

        /// <inheritdoc/>
        public int Order => 30;

        /// <inheritdoc/>
        public bool Evaluate(WorkflowRuleContext context)
        {
            return context?.Object?.AssigneeId is Guid assignee && assignee != Guid.Empty;
        }
    }

    /// <summary>
    /// The screen of the transition has to carry a note.
    /// </summary>
    /// <remarks>
    /// This is the validator that makes screens worth having: the note is read from what was
    /// typed into the transition, before anything is written, so a move that demands a reason
    /// cannot be made without one. It looks for a screen field named <c>note</c>, <c>comment</c>
    /// or <c>description</c> - the three names a screen of this kind is given - so an
    /// administrator does not have to match a magic string exactly.
    /// </remarks>
    public sealed class ScreenNoteGivenValidator : IWorkflowValidator
    {
        /// <summary>
        /// The screen field names read as the note of a transition, most specific first.
        /// </summary>
        internal static readonly string[] NoteFields = ["note", "comment", "kommentar", "description", "beschreibung", "reason", "begründung"];

        /// <inheritdoc/>
        public string Key => "screen.note.given";

        /// <inheritdoc/>
        public string Label => "kleenestar.core:workflow.rule.screen.note.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:workflow.rule.screen.note.description";

        /// <inheritdoc/>
        public string Message => "kleenestar.core:workflow.rule.screen.note.message";

        /// <inheritdoc/>
        public IIcon Icon => new IconComment();

        /// <inheritdoc/>
        public int Order => 40;

        /// <inheritdoc/>
        public bool Evaluate(WorkflowRuleContext context)
        {
            return !string.IsNullOrWhiteSpace(ReadNote(context));
        }

        /// <summary>
        /// Returns what the screen carries as its note, or <see langword="null"/>.
        /// </summary>
        /// <param name="context">The move being made.</param>
        /// <returns>The note.</returns>
        internal static string ReadNote(WorkflowRuleContext context)
        {
            return NoteFields
                .Select(context.Screen)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        }
    }

    /// <summary>
    /// The object has to carry at least one file.
    /// </summary>
    /// <remarks>
    /// The condition behind "no release without the signed-off report": the evidence is an
    /// attachment, and the move is refused while there is none.
    /// </remarks>
    public sealed class AttachmentPresentValidator : IWorkflowValidator
    {
        /// <inheritdoc/>
        public string Key => "attachment.present";

        /// <inheritdoc/>
        public string Label => "kleenestar.core:workflow.rule.attachment.present.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:workflow.rule.attachment.present.description";

        /// <inheritdoc/>
        public string Message => "kleenestar.core:workflow.rule.attachment.present.message";

        /// <inheritdoc/>
        public IIcon Icon => new IconPaperClip();

        /// <inheritdoc/>
        public int Order => 50;

        /// <inheritdoc/>
        public bool Evaluate(WorkflowRuleContext context)
        {
            return context?.Object is not null
                && (CoreHub.AttachmentManager?.GetAttachments(context.Object.Id)?.Any() ?? false);
        }
    }
}
