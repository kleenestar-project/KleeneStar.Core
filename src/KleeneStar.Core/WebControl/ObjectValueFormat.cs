using System;
using System.Globalization;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    // The entity type name collides with the KleeneStar.Core.WWW.Field namespace segment of
    // the same name; alias it inside the namespace block so Field resolves to the model
    // entity here.
    using Field = KleeneStar.Model.Entities.Field;
    using FieldType = KleeneStar.Model.Entities.FieldType;

    /// <summary>
    /// Turns a persisted field value into the line a reading view shows. Shared by every
    /// surface that presents values without an editor around them - the reduced object view
    /// beside a list and the unchangeable view of a form-rendered object - so the two never
    /// disagree about what a date or a boolean looks like.
    /// </summary>
    public static class ObjectValueFormat
    {
        /// <summary>
        /// The stand-in for a value a reading view is asked to show and does not have.
        /// </summary>
        public const string Empty = "—";

        /// <summary>
        /// Determines whether the field carries markup that has to be handed to the client
        /// to lay out rather than printed as a line of text.
        /// </summary>
        /// <param name="field">The field to test. May be null.</param>
        /// <returns>True for rich text.</returns>
        public static bool IsRichText(Field field)
        {
            return field?.FieldType == FieldType.RichText;
        }

        /// <summary>
        /// Formats a persisted value for reading: booleans as yes/no, dates in the
        /// visitor's culture, tag lists as a comma-separated line, secrets masked, and
        /// everything else as it is stored.
        /// </summary>
        /// <param name="renderContext">The render context, carrying the culture.</param>
        /// <param name="field">The field being formatted.</param>
        /// <param name="data">The persisted payload. May be null or blank, which formats
        /// as <see cref="Empty"/>.</param>
        /// <returns>The display text.</returns>
        public static string Format(IRenderControlContext renderContext, Field field, string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return Empty;
            }

            var culture = renderContext?.Request?.Culture ?? CultureInfo.InvariantCulture;

            switch (field?.FieldType)
            {
                case FieldType.Boolean:
                    return I18N.Translate(renderContext, bool.TryParse(data, out var flag) && flag
                        ? "kleenestar.core:object.property.yes"
                        : "kleenestar.core:object.property.no");

                case FieldType.Date:
                    // the value is stored round-trippable; it is read in the visitor's language,
                    // so it is written in the visitor's culture as well
                    return DateTime.TryParse(data, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
                        ? date.ToString("g", culture)
                        : data;

                case FieldType.Tag:
                    return string.Join(", ", data.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

                case FieldType.Password:
                    // a reading view is the one place a secret has no reason to appear at all;
                    // that the field is filled is the whole of what it says here
                    return new string('•', 8);

                case FieldType.Workflow:
                    // a workflow value is the state key the transition wrote ("in_progress"),
                    // not something anybody typed. The reduced pane never had to translate one
                    // because it drops workflow fields - the status sits in its headline - but
                    // the sheet of a form-rendered object prints every line the form asks for,
                    // and an internal key on it reads as a defect
                    return ResolveStatusName(field, data) ?? data;

                default:
                    return data;
            }
        }

        /// <summary>
        /// Resolves the display name of the workflow state a value names.
        /// </summary>
        /// <remarks>
        /// The same resolution the status badge does
        /// (<c>ObjectMetadataStatusFragment.BuildStatusBadge</c>): the field names its
        /// workflow, the workflow is loaded with its structure because the states are what
        /// is being looked up, and the payload is matched against them.
        /// </remarks>
        /// <param name="field">The workflow-typed field. May carry no workflow.</param>
        /// <param name="data">The persisted state key.</param>
        /// <returns>The state name, or <see langword="null"/> when it cannot be resolved.</returns>
        private static string ResolveStatusName(Field field, string data)
        {
            if (field?.WorkflowId is not Guid workflowId)
            {
                return null;
            }

            var workflow = CoreHub.WorkflowManager?.GetWorkflowWithStructure(workflowId);

            return workflow is null
                ? null
                : CoreHub.WorkflowManager.ResolveStatus(workflow, data)?.Name;
        }
    }
}
