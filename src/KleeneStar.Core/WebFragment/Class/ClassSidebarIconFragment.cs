using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WWW.Class._classid_;
using System;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Class
{
    /// <summary>
    /// Represents a sidebar icon fragment for a class, providing rendering and 
    /// editing capabilities within the class sidebar.
    /// </summary>
    [Section<SectionSidebarPreferences>]
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
    public sealed class ClassSidebarIconFragment : FragmentControlSidebarItemIcon
    {
        private readonly IClassManager _classManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for its operation. 
        /// Cannot be null.
        /// </param>
        /// <param name="classManager">
        /// The workspace manager used to retrieve workspace information. Cannot be null.
        /// </param>
        public ClassSidebarIconFragment(IFragmentContext fragmentContext, IClassManager classManager)
            : base(fragmentContext)
        {
            _classManager = classManager;

            IconEdit = _ => true;
            Icon = renderContext => GetIcon(renderContext);
            PrimaryAction = renderContext => new ActionModal("modal-form", GetUri(renderContext));
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
        /// Retrieves a URI for the current request based on class, form, or workflow parameters 
        /// present in the render context.
        /// </summary>
        /// <param name="renderContext">
        /// The context containing the request and its parameters used to determine the appropriate 
        /// URI. Cannot be null.
        /// </param>
        /// <returns>
        /// An object representing the URI associated with the specified class, form, or workflow 
        /// parameters.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if none of the required parameters ('class', 'form', or 'workflow') are present 
        /// in the request, or if a referenced form or workflow cannot be found.
        /// </exception>
        private IUri GetUri(IRenderControlContext renderContext)
        {
            var classParameter = renderContext.Request.GetParameter<ClassIdParameter>();
            var formParameter = renderContext.Request.GetParameter<FormIdParameter>();
            var workflowParameter = renderContext.Request.GetParameter<WorkflowIdParameter>();

            // ensure that at least one of the required parameters is present
            if (classParameter == null && formParameter == null && workflowParameter == null)
            {
                throw new InvalidOperationException("One of the parameters 'class', 'form' or 'workflow' must be set.");
            }

            // normalize the class parameter
            if (classParameter == null && formParameter != null)
            {
                var formId = Guid.TryParse(formParameter.Value, out var formGuid)
                    ? formGuid
                    : Guid.Empty;

                // an id from the url may address a form that no longer exists; the class
                // is then simply unresolved rather than the render being aborted
                classParameter = CoreHub.FormManager.GetForm(formId) is { } form
                    ? new ClassIdParameter(form.ClassId.ToString())
                    : null;
            }
            else if (classParameter == null && workflowParameter != null)
            {
                var workflowId = Guid.TryParse(workflowParameter.Value, out var formGuid)
                    ? formGuid
                    : Guid.Empty;

                classParameter = CoreHub.WorkflowManager.GetWorkflow(workflowId) is { } workflow
                    ? new ClassIdParameter(workflow.ClassId.ToString())
                    : null;
            }

            var uri = CoreHub.GetUri<Avatar>()?
                .BindParameters(classParameter);

            return uri;
        }

        /// <summary>
        /// Retrieves the icon associated with the class specified in the current 
        /// render context.
        /// </summary>
        /// <param name="renderContext">
        /// The rendering context containing the request parameters used to identify 
        /// the class.
        /// </param>
        /// <returns>
        /// The icon for the specified class, or null if the class is not found or 
        /// does not have an associated icon.
        /// </returns>
        private IIcon GetIcon(IRenderControlContext renderContext)
        {
            var classParameter = renderContext.Request.GetParameter<ClassIdParameter>();
            var formParameter = renderContext.Request.GetParameter<FormIdParameter>();
            var workflowParameter = renderContext.Request.GetParameter<WorkflowIdParameter>();

            // ensure that at least one of the required parameters is present
            if (classParameter == null && formParameter == null && workflowParameter == null)
            {
                throw new InvalidOperationException("One of the parameters 'class', 'form' or 'workflow' must be set.");
            }

            // normalize the class parameter
            if (classParameter == null && formParameter != null)
            {
                var formId = Guid.TryParse(formParameter.Value, out var formGuid)
                    ? formGuid
                    : Guid.Empty;

                // an id from the url may address a form that no longer exists; the class
                // is then simply unresolved rather than the render being aborted
                classParameter = CoreHub.FormManager.GetForm(formId) is { } form
                    ? new ClassIdParameter(form.ClassId.ToString())
                    : null;
            }
            else if (classParameter == null && workflowParameter != null)
            {
                var workflowId = Guid.TryParse(workflowParameter.Value, out var formGuid)
                    ? formGuid
                    : Guid.Empty;

                classParameter = CoreHub.WorkflowManager.GetWorkflow(workflowId) is { } workflow
                    ? new ClassIdParameter(workflow.ClassId.ToString())
                    : null;
            }

            var guid = Guid.TryParse(classParameter?.Value, out var classGuid)
                ? classGuid
                : Guid.Empty;
            var @class = _classManager.GetClass(guid);

            return @class?.Icon;
        }
    }
}
