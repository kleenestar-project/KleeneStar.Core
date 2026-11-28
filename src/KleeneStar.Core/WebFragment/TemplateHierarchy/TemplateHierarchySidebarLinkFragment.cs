using KleeneStar.Core.WebFragment.Template;
using KleeneStar.Core.WebPolicies;
using System;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebFragment.TemplateHierarchy
{
    /// <summary>
    /// The sidebar link of the template and hierarchy overviews that leads to the hierarchies of
    /// the workspace; its badge counts them.
    /// </summary>
    [Section<SectionSidebarPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Templates._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Templates._workspacekey_.Hierarchies>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceViewPolicy>>]
    [Order(1)]
    [Cache]
    public sealed class TemplateHierarchySidebarLinkFragment : TemplateAreaSidebarLinkFragment
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public TemplateHierarchySidebarLinkFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Icon = _ => new IconSitemap();
            Text = _ => "kleenestar.core:template.hierarchy.link.label";
        }

        /// <summary>
        /// Returns the address of the hierarchy overview.
        /// </summary>
        /// <returns>The address.</returns>
        protected override IUri GetOverviewUri()
        {
            return CoreHub.GetUri<global::KleeneStar.Core.WWW.Templates._workspacekey_.Hierarchies>();
        }

        /// <summary>
        /// Counts the hierarchies of the workspace, as the hierarchy table lists them.
        /// </summary>
        /// <param name="workspaceId">The workspace.</param>
        /// <returns>The count.</returns>
        protected override int Count(Guid workspaceId)
        {
            return CoreHub.TemplateHierarchyManager
                .GetHierarchies(new Query<Model.Entities.TemplateHierarchy>().Where(x => x.WorkspaceId == workspaceId))
                .Count();
        }
    }
}
