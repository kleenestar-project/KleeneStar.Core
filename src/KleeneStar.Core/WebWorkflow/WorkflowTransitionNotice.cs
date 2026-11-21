using KleeneStar.Core.WebManager;
using System.Globalization;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebWorkflow
{
    /// <summary>
    /// Tells the user why a state change they asked for did not happen.
    /// </summary>
    /// <remarks>
    /// Two places move an object, and they tell the reason differently. The state dropdown of
    /// the workflow card redirects back to an unchanged page, which carries no text, so its
    /// reason travels as a toast (<see cref="Report"/>). A drop on the Kanban board is refused
    /// through the board's own answer, which shows a <c>RestApiRefusal</c>'s message where the
    /// card snaps back (<see cref="Explain"/>).
    /// </remarks>
    internal static class WorkflowTransitionNotice
    {
        /// <summary>
        /// Returns the sentence a refused state change is explained with, in the language of
        /// the request.
        /// </summary>
        /// <param name="result">The outcome reported by the workflow manager.</param>
        /// <param name="request">The request whose culture the sentence is written in.</param>
        /// <returns>The translated sentence, naming what blocks the move where a relation does.</returns>
        public static string Explain(WorkflowTransitionResult result, IRequest request)
        {
            return result.ValidationErrors is { Count: > 0 }
                ? I18N.Translate(request, result.Message, string.Join(", ", result.ValidationErrors))
                : I18N.Translate(request, result.Message);
        }

        /// <summary>
        /// Surfaces a refused state change as a toast. A change that went through stays silent:
        /// it is visible where the user looks next, and stamping the object already raises the
        /// "object updated" toast. A no-op change is not worth a toast either.
        /// </summary>
        /// <param name="result">The outcome reported by the workflow manager.</param>
        public static void Report(WorkflowTransitionResult result)
        {
            if (result is null || result.Succeeded || result.Outcome == WorkflowTransitionOutcome.Unchanged)
            {
                return;
            }

            CoreHub.AddNotification
            (
                "kleenestar.core:notification.title.error",
                Describe(result),
                5000
            );
        }

        /// <summary>
        /// Builds the sentence a refused state change is reported with. A move a relation
        /// refused names what has to happen first, because "not allowed" would leave the user
        /// looking for a workflow rule that is not the reason.
        /// </summary>
        /// <remarks>
        /// The message is composed here rather than by the manager: it is translated and filled
        /// in one step, and the notification pipeline translates a key it is given while passing
        /// finished prose through unchanged.
        /// </remarks>
        /// <param name="result">The outcome reported by the workflow manager.</param>
        /// <returns>The message key, or the composed sentence.</returns>
        public static string Describe(WorkflowTransitionResult result)
        {
            if (result.Outcome != WorkflowTransitionOutcome.Blocked || result.ValidationErrors is not { Count: > 0 })
            {
                return result.Message;
            }

            return string.Format
            (
                CultureInfo.CurrentCulture,
                I18N.Translate(result.Message),
                string.Join(", ", result.ValidationErrors)
            );
        }
    }
}
