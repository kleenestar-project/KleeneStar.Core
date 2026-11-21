using KleeneStar.Core.WebParameter;
using System;
using System.Linq;
using WebExpress.WebApp.WebRelation;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Class
{
    /// <summary>
    /// Represents a sidebar item link fragment that displays the 'relations' link in the class
    /// sidebar.
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
    [Cache]
    public sealed class ClassSidebarRelationLinkFragment : FragmentControlSidebarItemLink
    {
        private static readonly IUri _uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Relations._classid_.Index>();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for
        /// its operation. Cannot be null.
        /// </param>
        public ClassSidebarRelationLinkFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Icon = _ => new IconLink();
            Text = _ => "kleenestar.core:relation.link.label";
            Uri = renderContext => GetUri(renderContext);
            Active = renderContext => IsActive(renderContext)
                ? TypeActive.Active
                : TypeActive.None;
            Badge = renderContext => GetBadge(renderContext);
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Resolves the class identifier from the render context. The relation administration
        /// is reachable from every class-scoped page, so the id is taken from whichever of the
        /// class-bearing route parameters the current request carries.
        /// </summary>
        /// <param name="renderContext">The render context. Cannot be null.</param>
        /// <returns>The class id, or <see cref="Guid.Empty"/> when none can be resolved.</returns>
        private static Guid ResolveClassId(IRenderControlContext renderContext)
        {
            var classParameter = renderContext.Request.GetParameter<ClassIdParameter>();
            if (classParameter != null)
            {
                return Guid.TryParse(classParameter.Value, out var parsed) ? parsed : Guid.Empty;
            }

            var formParameter = renderContext.Request.GetParameter<FormIdParameter>();
            if (formParameter != null)
            {
                // an id from the url may address a form that no longer exists; the class is
                // then simply unresolved rather than the render being aborted
                return Guid.TryParse(formParameter.Value, out var formId)
                    ? CoreHub.FormManager.GetForm(formId)?.ClassId ?? Guid.Empty
                    : Guid.Empty;
            }

            var workflowParameter = renderContext.Request.GetParameter<WorkflowIdParameter>();
            if (workflowParameter != null)
            {
                return Guid.TryParse(workflowParameter.Value, out var workflowId)
                    ? CoreHub.WorkflowManager.GetWorkflow(workflowId)?.ClassId ?? Guid.Empty
                    : Guid.Empty;
            }

            return Guid.Empty;
        }

        /// <summary>
        /// Determines whether the current request addresses the relation administration.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns><c>true</c> when the link points at the page being shown.</returns>
        private static bool IsActive(IRenderControlContext renderContext)
        {
            var target = string.Join("/", GetUri(renderContext).BindParameters(renderContext.Request).PathSegments ?? []);
            var current = string.Join("/", renderContext.Request.Uri.PathSegments ?? []);

            return string.Equals(current, target, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Retrieves the relation administration route for the resolved class.
        /// </summary>
        /// <param name="renderContext">The render context. Cannot be null.</param>
        /// <returns>The bound route.</returns>
        private static IUri GetUri(IRenderControlContext renderContext)
        {
            return _uri.BindParameters(new ClassIdParameter(ResolveClassId(renderContext)));
        }

        /// <summary>
        /// Computes the trailing sidebar badge: the number of relations the class may hold.
        /// Returns <c>null</c> when the class accepts none, so an empty catalog leaves the link
        /// badge-free rather than showing a noisy zero.
        /// </summary>
        /// <param name="renderContext">The render context. Cannot be null.</param>
        /// <returns>The count as a string, or <c>null</c> when there is nothing to display.</returns>
        private static string GetBadge(IRenderControlContext renderContext)
        {
            var classId = ResolveClassId(renderContext);
            if (classId == Guid.Empty)
            {
                return null;
            }

            var name = CoreHub.ClassManager.GetClass(classId)?.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            // the badge counts what the class may actually hold, which is the relations that
            // accept it - a relation that names no classes at all accepts every one of them
            var count = RelationRegistry.Types
                .Where(x => x.Active)
                .Count(x => !x.TargetClasses.Any() || x.TargetClasses.Contains(name, StringComparer.OrdinalIgnoreCase));

            return count > 0 ? count.ToString() : null;
        }
    }
}
