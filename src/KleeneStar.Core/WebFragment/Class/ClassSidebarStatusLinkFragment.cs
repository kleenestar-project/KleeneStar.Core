using KleeneStar.Core.WebParameter;
using System;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Class
{
    /// <summary>
    /// Represents a sidebar item link fragment that displays the 'workflow status' link in the class sidebar.
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
    public sealed class ClassSidebarStatusLinkFragment : FragmentControlSidebarItemLink
    {
        private static readonly IUri _uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Statuses._classid_.Index>();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for its operation. 
        /// Cannot be null.
        /// </param>
        public ClassSidebarStatusLinkFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Icon = _ => new IconStatus();
            Text = _ => "kleenestar.core:status.link.label";
            Uri = renderContext => GetUri(renderContext);
            Active = renderContext => IsActive(renderContext)
                ? TypeActive.Active
                : TypeActive.None;
            Badge = renderContext => GetBadge(renderContext);
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">
        /// The context in which the control is rendered.
        /// </param>
        /// <param name="visualTree">
        /// The visual tree representing the control's structure.
        /// </param>
        /// <returns>
        /// An HTML node representing the rendered control.
        /// </returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Resolves the class identifier from the specified render context based on 
        /// request parameters.
        /// </summary>
        /// <param name="renderContext">
        /// The render context containing the request parameters used to determine the 
        /// class identifier. Cannot be null.
        /// </param>
        /// <returns>
        /// A id representing the resolved class identifier. Returns empty if the
        /// class parameter is present but cannot be parsed.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if neither a valid class nor form parameter is present in the request, 
        /// or if the specified form cannot be found.
        /// </exception>
        private static Guid ResolveClassId(IRenderControlContext renderContext)
        {
            var classParam = renderContext.Request.GetParameter<ClassIdParameter>();
            var formParam = renderContext.Request.GetParameter<FormIdParameter>();
            var workflowParameter = renderContext.Request.GetParameter<WorkflowIdParameter>();
            // add more params here in the future

            if (classParam != null)
            {
                return Guid.TryParse(classParam.Value, out var parsed)
                    ? parsed
                    : Guid.Empty;
            }

            if (formParam != null)
            {
                var formId = Guid.TryParse(formParam.Value, out var parsedForm)
                    ? parsedForm
                    : Guid.Empty;

                // an id from the url may address a form that no longer exists; the class
                // is then simply unresolved rather than the render being aborted
                return CoreHub.FormManager.GetForm(formId)?.ClassId ?? Guid.Empty;
            }
            else if (workflowParameter != null)
            {
                var workflowId = Guid.TryParse(workflowParameter.Value, out var formGuid)
                    ? formGuid
                    : Guid.Empty;

                return CoreHub.WorkflowManager.GetWorkflow(workflowId)?.ClassId ?? Guid.Empty;
            }

            throw new InvalidOperationException("One of the parameters 'class','form' or 'workflow' must be set.");
        }

        /// <summary>
        /// Determines whether the current request URI matches any of the predefined 
        /// target URIs for the specified class.
        /// </summary>
        /// <param name="renderContext">
        /// The rendering context that provides information about the current 
        /// HTTP request.
        /// </param>
        /// <returns>
        /// true if the current request URI matches one of the target URIs; 
        /// otherwise, false.
        /// </returns>
        private bool IsActive(IRenderControlContext renderContext)
        {
            var targetUris = new[]
            {
                GetUri(renderContext),
                // add more uris here in the future
            }
                .Select(x => x.BindParameters(new ClassIdParameter(ResolveClassId(renderContext))))
                .Select(x => x.BindParameters(renderContext.Request))
                .Select(x => string.Join("/", x.PathSegments ?? []));

            var currentUri = string.Join("/", renderContext.Request.Uri.PathSegments ?? []);

            return targetUris.Any(uri => string.Equals(currentUri, uri, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Retrieves a URI instance for the specified render context, binding the resolved class 
        /// identifier as a parameter.
        /// </summary>
        /// <param name="renderContext">
        /// The render context used to resolve the class identifier and generate the corresponding 
        /// URI. Cannot be null.
        /// </param>
        /// <returns>
        /// An IUri instance representing the resource associated with the resolved class identifier.
        /// </returns>
        private static IUri GetUri(IRenderControlContext renderContext)
        {
            var classId = ResolveClassId(renderContext);

            // bind the classId into the main URI
            return _uri.BindParameters(new ClassIdParameter(classId));
        }

        /// <summary>
        /// Computes the trailing sidebar badge for the 'workflow status' link: the number of
        /// statuses across the resolved class's workflows. Returns <c>null</c> when no class can
        /// be resolved or the class has no statuses, so an empty collection leaves the link
        /// badge-free rather than showing a noisy zero.
        /// </summary>
        /// <param name="renderContext">
        /// The render context used to resolve the class identifier whose statuses are counted.
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

            var count = CoreHub.StatusManager.GetStatuses(new ClassIdParameter(classId)).Count();

            return count > 0 ? count.ToString() : null;
        }
    }
}
