using KleeneStar.Core.WebParameter;
using System;
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
    /// The wizard that edits a template hierarchy.
    /// </summary>
    [Title("kleenestar.core:template.hierarchy.edit.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.TemplateHierarchy._hierarchyid_.Edit>]
    [Cache]
    public sealed class TemplateHierarchyEditFormFragment : TemplateHierarchyWizardFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public TemplateHierarchyEditFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Mode = _ => TypeRestFormMode.Edit;
            FinishLabel = _ => "kleenestar.core:template.hierarchy.wizard.finish.edit";
            FinishIcon = _ => new IconSave();
            ItemId = renderContext => ResolveHierarchy(renderContext)?.Id.ToString();
        }

        /// <summary>
        /// Returns the hierarchy the route names.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <returns>The hierarchy, or null.</returns>
        protected override TemplateHierarchyEntity ResolveHierarchy(IRenderControlContext renderContext)
        {
            var value = renderContext?.Request?.GetParameter<TemplateHierarchyIdParameter>()?.Value;

            return Guid.TryParse(value, out var id) ? CoreHub.TemplateHierarchyManager.GetHierarchy(id) : null;
        }

        /// <summary>
        /// Returns the workspace of the hierarchy.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="hierarchy">The hierarchy.</param>
        /// <returns>The workspace, or null.</returns>
        protected override WorkspaceEntity ResolveWorkspace(IRenderControlContext renderContext, TemplateHierarchyEntity hierarchy)
        {
            return hierarchy is null ? null : hierarchy.Workspace ?? CoreHub.WorkspaceManager.GetWorkspace(hierarchy.WorkspaceId);
        }
    }
}
