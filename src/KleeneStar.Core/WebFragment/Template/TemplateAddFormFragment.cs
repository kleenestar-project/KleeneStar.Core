using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

using TemplateEntity = KleeneStar.Model.Entities.Template;
using WorkspaceEntity = KleeneStar.Model.Entities.Workspace;

namespace KleeneStar.Core.WebFragment.Template
{
    /// <summary>
    /// Represents the wizard a template is created with (<see cref="TemplateWizardFragmentBase"/>),
    /// offering the classes of the workspace the route names.
    /// </summary>
    [Title("kleenestar.core:template.add.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Templates._workspacekey_.Add>]
    [Cache]
    public sealed class TemplateAddFormFragment : TemplateWizardFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public TemplateAddFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Mode = _ => TypeRestFormMode.Add;
            FinishLabel = _ => "kleenestar.core:template.wizard.finish.add";
            FinishIcon = _ => new IconPlus();
        }

        /// <summary>
        /// Returns no template: one is being created.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <returns>Always null.</returns>
        protected override TemplateEntity ResolveTemplate(IRenderControlContext renderContext)
        {
            return null;
        }

        /// <summary>
        /// Returns the workspace the route names.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="template">Not used.</param>
        /// <returns>The workspace, or null.</returns>
        protected override WorkspaceEntity ResolveWorkspace(IRenderControlContext renderContext, TemplateEntity template)
        {
            var key = renderContext?.Request?.GetParameter<WorkspaceKeyParameter>()?.Value;

            return string.IsNullOrWhiteSpace(key) ? null : CoreHub.WorkspaceManager.GetWorkspaceByKey(key);
        }
    }
}
