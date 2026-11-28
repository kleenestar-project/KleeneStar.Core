using KleeneStar.Core.WebFragment.Template;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebPolicies;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebFragment.TemplateHierarchy
{
    /// <summary>
    /// The category section of the hierarchy sidebar: one entry per category the workspace's
    /// hierarchies use. Selecting an entry narrows the hierarchy table to that category.
    /// </summary>
    [Section<SectionSidebarSecondary>]
    [Scope<global::KleeneStar.Core.WWW.Templates._workspacekey_.Hierarchies>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceViewPolicy>>]
    [Cache]
    public sealed class TemplateHierarchySidebarCategoryFragment : TemplateSidebarCategoryFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        /// <param name="workspaceManager">The workspace manager used to resolve the workspace.</param>
        public TemplateHierarchySidebarCategoryFragment(IFragmentContext fragmentContext, IWorkspaceManager workspaceManager)
            : base(fragmentContext, workspaceManager)
        {
        }

        /// <summary>
        /// Returns the i18n key of the entry that clears the selection.
        /// </summary>
        protected override string AllLabel => "kleenestar.core:template.hierarchy.quickfilter.all.label";

        /// <summary>
        /// Returns the category values of the hierarchies of the workspace.
        /// </summary>
        /// <param name="workspaceId">The workspace.</param>
        /// <returns>The category values.</returns>
        protected override IEnumerable<string> GetCategoryValues(Guid workspaceId)
        {
            return CoreHub.TemplateHierarchyManager
                .GetHierarchies(new Query<Model.Entities.TemplateHierarchy>().Where(x => x.WorkspaceId == workspaceId))
                .Select(x => x.Category);
        }
    }
}
