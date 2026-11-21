using KleeneStar.Core.WebParameter;
using System;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebUri;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Class
{
    /// <summary>
    /// Sidebar item that links to the SLA-policy overview page of the active class.
    /// </summary>
    [Section<SectionSidebarPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Class._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Fields._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.SecurityLevels._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Forms._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Priorities._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Workflows._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Statuses._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Slas._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Calendars._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Relations._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Form._formid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Workflow._workflowid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Sla._slaid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Calendar._calendarid_.Index>]
    [Condition<ClassServiceLevelCondition>]
    [Order(20)]
    [Cache]
    public sealed class ClassSidebarSlaLinkFragment : FragmentControlSidebarItemLink
    {
        private static readonly IUri _uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Slas._classid_.Index>();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        public ClassSidebarSlaLinkFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Icon = _ => new IconProcessTimer();
            Text = _ => "kleenestar.core:sla.link.label";
            Uri = renderContext => GetUri(renderContext);
            Active = renderContext => IsActive(renderContext)
                ? TypeActive.Active
                : TypeActive.None;
            Badge = renderContext => GetBadge(renderContext);
        }

        /// <summary>
        /// Converts the fragment to HTML.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Resolves the class id from the active render context, walking through SLA/Form/Workflow
        /// parameters if no class parameter is present.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The resolved class id, or <see cref="Guid.Empty"/>.</returns>
        private static Guid ResolveClassId(IRenderControlContext renderContext)
        {
            var classParam = renderContext.Request.GetParameter<ClassIdParameter>();
            var slaParam = renderContext.Request.GetParameter<SlaIdParameter>();
            var formParam = renderContext.Request.GetParameter<FormIdParameter>();
            var workflowParameter = renderContext.Request.GetParameter<WorkflowIdParameter>();

            if (classParam != null)
            {
                return Guid.TryParse(classParam.Value, out var parsed) ? parsed : Guid.Empty;
            }

            if (slaParam != null)
            {
                var slaId = Guid.TryParse(slaParam.Value, out var parsed) ? parsed : Guid.Empty;
                var policy = CoreHub.SlaManager.GetSla(slaId);
                return policy?.ClassId ?? Guid.Empty;
            }

            if (formParam != null)
            {
                var formId = Guid.TryParse(formParam.Value, out var parsed) ? parsed : Guid.Empty;
                var form = CoreHub.FormManager.GetForm(formId);
                return form?.ClassId ?? Guid.Empty;
            }

            if (workflowParameter != null)
            {
                var workflowId = Guid.TryParse(workflowParameter.Value, out var parsed) ? parsed : Guid.Empty;
                var workflow = CoreHub.WorkflowManager.GetWorkflow(workflowId);
                return workflow?.ClassId ?? Guid.Empty;
            }

            return Guid.Empty;
        }

        /// <summary>
        /// Indicates whether the current request URI points at this SLA page.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns><c>true</c> when the page is active.</returns>
        private bool IsActive(IRenderControlContext renderContext)
        {
            var targetUris = new[]
            {
                GetUri(renderContext),
            }
                .Select(x => x.BindParameters(new ClassIdParameter(ResolveClassId(renderContext))))
                .Select(x => x.BindParameters(renderContext.Request))
                .Select(x => string.Join("/", x.PathSegments ?? []));

            var currentUri = string.Join("/", renderContext.Request.Uri.PathSegments ?? []);

            return targetUris.Any(uri => string.Equals(currentUri, uri, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns the resolved SLA-page URI for the current class.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The bound URI.</returns>
        private static IUri GetUri(IRenderControlContext renderContext)
        {
            var classId = ResolveClassId(renderContext);

            return _uri.BindParameters(new ClassIdParameter(classId));
        }

        /// <summary>
        /// Computes the trailing sidebar badge for the 'SLA' link: the number of SLA policies
        /// attached to the resolved class. Returns <c>null</c> when no class can be resolved or
        /// the class has no SLA policies, so an empty collection leaves the link badge-free
        /// rather than showing a noisy zero.
        /// </summary>
        /// <param name="renderContext">
        /// The render context used to resolve the class identifier whose SLA policies are counted.
        /// Cannot be null.
        /// </param>
        /// <returns>
        /// The element count as a string, or <c>null</c> when there is nothing to display.
        /// </returns>
        private static string GetBadge(IRenderControlContext renderContext)
        {
            var classId = ResolveClassId(renderContext);
            if (classId == Guid.Empty)
            {
                return null;
            }

            var count = CoreHub.SlaManager.GetSlas(new ClassIdParameter(classId)).Count();

            return count > 0 ? count.ToString() : null;
        }
    }
}
