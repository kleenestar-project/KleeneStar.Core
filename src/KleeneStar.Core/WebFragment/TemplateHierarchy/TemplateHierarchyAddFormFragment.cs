using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

using TemplateHierarchyEntity = KleeneStar.Model.Entities.TemplateHierarchy;
using WorkspaceEntity = KleeneStar.Model.Entities.Workspace;

namespace KleeneStar.Core.WebFragment.TemplateHierarchy
{
    /// <summary>
    /// The wizard that adds a template hierarchy to the workspace of the template overview.
    /// </summary>
    [Title("kleenestar.core:template.hierarchy.add.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Templates._workspacekey_.AddHierarchy>]
    [Cache]
    public sealed class TemplateHierarchyAddFormFragment : TemplateHierarchyWizardFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public TemplateHierarchyAddFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Mode = _ => TypeRestFormMode.Add;
            FinishLabel = _ => "kleenestar.core:template.hierarchy.wizard.finish.add";
            FinishIcon = _ => new IconPlus();
        }

        /// <summary>
        /// Returns null: a hierarchy is being created.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <returns>Null.</returns>
        protected override TemplateHierarchyEntity ResolveHierarchy(IRenderControlContext renderContext)
        {
            return null;
        }

        /// <summary>
        /// Returns the workspace the route names.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="hierarchy">Null.</param>
        /// <returns>The workspace, or null.</returns>
        protected override WorkspaceEntity ResolveWorkspace(IRenderControlContext renderContext, TemplateHierarchyEntity hierarchy)
        {
            var key = renderContext?.Request?.GetParameter<WorkspaceKeyParameter>()?.Value;

            return string.IsNullOrWhiteSpace(key) ? null : CoreHub.WorkspaceManager.GetWorkspaceByKey(key);
        }
    }
}
