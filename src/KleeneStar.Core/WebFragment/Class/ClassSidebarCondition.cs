using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebParameter;
using System;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebFragment.Class
{
    /// <summary>
    /// Shared base for the conditions that decide which links the class administration sidebar
    /// offers for the class its route addresses.
    /// </summary>
    /// <remarks>
    /// The sidebar stands on the class page and on every page beneath it, so the class is named
    /// by whichever segment the route carries: the class itself, or a form, workflow, SLA or
    /// calendar of it. A route whose class cannot be resolved answers <see langword="true"/> -
    /// the link then behaves as it did before anybody asked, and the page reports the missing
    /// record on its own.
    /// </remarks>
    public abstract class ClassSidebarCondition : ICondition
    {
        /// <summary>
        /// Determines whether the condition is fulfilled for the request.
        /// </summary>
        /// <param name="request">The request whose route names the class.</param>
        /// <returns><see langword="true"/> when the link belongs in the sidebar.</returns>
        public bool Fulfillment(IRequest request)
        {
            var @class = ResolveClass(request);

            return @class is null || Offers(@class);
        }

        /// <summary>
        /// Answers whether the sidebar offers the link for the supplied class.
        /// </summary>
        /// <param name="class">The class the route addresses. Never null.</param>
        /// <returns><see langword="true"/> when the link belongs in the sidebar.</returns>
        protected abstract bool Offers(Model.Entities.Class @class);

        /// <summary>
        /// Resolves the class the route addresses, directly or through one of its forms,
        /// workflows, SLAs or calendars.
        /// </summary>
        /// <param name="request">The request whose route names the class.</param>
        /// <returns>The class, or <see langword="null"/> when the route names none that
        /// exists.</returns>
        internal static Model.Entities.Class ResolveClass(IRequest request)
        {
            if (request is null)
            {
                return null;
            }

            var classId = Parse(request.GetParameter<ClassIdParameter>()?.Value);

            classId ??= Parse(request.GetParameter<FormIdParameter>()?.Value) is { } formId
                ? CoreHub.FormManager.GetForm(formId)?.ClassId
                : null;

            classId ??= Parse(request.GetParameter<WorkflowIdParameter>()?.Value) is { } workflowId
                ? CoreHub.WorkflowManager.GetWorkflow(workflowId)?.ClassId
                : null;

            classId ??= Parse(request.GetParameter<SlaIdParameter>()?.Value) is { } slaId
                ? CoreHub.SlaManager.GetSla(slaId)?.ClassId
                : null;

            classId ??= Parse(request.GetParameter<CalendarIdParameter>()?.Value) is { } calendarId
                ? CoreHub.CalendarManager.GetCalendar(calendarId)?.ClassId
                : null;

            return classId is { } id
                ? CoreHub.ClassManager.GetClass(id)
                : null;
        }

        /// <summary>
        /// Parses a route segment as an id.
        /// </summary>
        /// <param name="value">The segment value. May be null.</param>
        /// <returns>The id, or <see langword="null"/> when the segment is absent, not an id,
        /// or the empty id.</returns>
        private static Guid? Parse(string value)
        {
            return Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
        }
    }

    /// <summary>
    /// Fulfilled when the kind of the addressed class is measured against service levels
    /// (<see cref="IObjectKind.ServiceLevels"/>) - it gates the SLA and calendar links, which
    /// the document, blog and asset kinds do not offer.
    /// </summary>
    public sealed class ClassServiceLevelCondition : ClassSidebarCondition
    {
        /// <inheritdoc/>
        protected override bool Offers(Model.Entities.Class @class)
        {
            // an uninstalled add-on kind keeps what every kind had before the question existed
            return ObjectKindCatalog.GetKind(@class.Kind)?.ServiceLevels ?? true;
        }
    }

    /// <summary>
    /// Fulfilled when the addressed class reads its objects through a structured mask rather
    /// than as prose - it gates the field and form links. A prose class has no mask a field
    /// could stand in or a form could arrange: a blog class always, a document class unless it
    /// was switched to the form renderer. The answer is the <em>effective</em> renderer
    /// (<see cref="ObjectRendererCatalog.IsRenderedAs"/>), so a class that names none follows
    /// its kind.
    /// </summary>
    public sealed class ClassStructuredRendererCondition : ClassSidebarCondition
    {
        /// <inheritdoc/>
        protected override bool Offers(Model.Entities.Class @class)
        {
            return !ObjectRendererCatalog.IsRenderedAs(@class, Model.Entities.ObjectRenderer.Prose);
        }
    }
}
