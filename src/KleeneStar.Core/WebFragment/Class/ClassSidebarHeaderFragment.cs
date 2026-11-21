using KleeneStar.Core.WebParameter;
using System;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Class
{
    /// <summary>
    /// Represents a sidebar header fragment that displays class-related information within 
    /// the user interface sidebar.
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
    public sealed class ClassSidebarHeaderFragment : FragmentControlSidebarItemHeader
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for its operation. 
        /// Cannot be null.
        /// </param>
        public ClassSidebarHeaderFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
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
            var classParameter = renderContext.Request.GetParameter<ClassIdParameter>();
            var formParameter = renderContext.Request.GetParameter<FormIdParameter>();
            var workflowParameter = renderContext.Request.GetParameter<WorkflowIdParameter>();

            // ensure that at least one of the required parameters is present
            if (classParameter == null && formParameter == null && workflowParameter == null)
            {
                throw new InvalidOperationException("One of the parameters 'class', 'form' or 'workflow' must be set.");
            }

            // normalize the class parameter. The ids arrive from the url, so one that
            // addresses nothing is an ordinary outcome — a stale bookmark, a reseeded
            // database — rather than a programming error, and the header then simply
            // carries no class name instead of taking the whole page render down with it.
            if (classParameter == null && formParameter != null)
            {
                var formId = Guid.TryParse(formParameter.Value, out var formGuid)
                    ? formGuid
                    : Guid.Empty;

                // create a synthetic class parameter based on the form's ClassId
                classParameter = CoreHub.FormManager.GetForm(formId) is { } form
                    ? new ClassIdParameter(form.ClassId.ToString())
                    : null;
            }
            else if (classParameter == null && workflowParameter != null)
            {
                var workflowId = Guid.TryParse(workflowParameter.Value, out var workflowGuid)
                    ? workflowGuid
                    : Guid.Empty;

                // create a synthetic class parameter based on the workflow's ClassId
                classParameter = CoreHub.WorkflowManager.GetWorkflow(workflowId) is { } workflow
                    ? new ClassIdParameter(workflow.ClassId.ToString())
                    : null;
            }

            var guid = Guid.TryParse(classParameter?.Value, out var classGuid)
                ? classGuid
                : Guid.Empty;
            var @class = CoreHub.ClassManager.GetClass(guid);

            return base.Render(renderContext, visualTree, @class?.Name);
        }
    }
}
