using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebPolicies;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebFragment.Template
{
    /// <summary>
    /// The category section of the template sidebar: one entry per category the workspace's
    /// templates use. Selecting an entry narrows the template table to that category.
    /// </summary>
    [Section<SectionSidebarSecondary>]
    [Scope<global::KleeneStar.Core.WWW.Templates._workspacekey_.Index>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceViewPolicy>>]
    [Cache]
    public sealed class TemplateSidebarCategoryFragment : TemplateSidebarCategoryFragmentBase
    {
        private readonly ITemplateManager _templateManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        /// <param name="templateManager">The template manager used to read the templates.</param>
        /// <param name="workspaceManager">The workspace manager used to resolve the workspace.</param>
        public TemplateSidebarCategoryFragment(IFragmentContext fragmentContext, ITemplateManager templateManager, IWorkspaceManager workspaceManager)
            : base(fragmentContext, workspaceManager)
        {
            _templateManager = templateManager;
        }

        /// <summary>
        /// Returns the i18n key of the entry that clears the selection.
        /// </summary>
        protected override string AllLabel => "kleenestar.core:template.quickfilter.all.label";

        /// <summary>
        /// Returns the category values of the templates of the workspace.
        /// </summary>
        /// <param name="workspaceId">The workspace.</param>
        /// <returns>The category values.</returns>
        protected override IEnumerable<string> GetCategoryValues(Guid workspaceId)
        {
            return _templateManager
                .GetTemplates(new Query<Model.Entities.Template>().Where(x => x.Class.WorkspaceId == workspaceId))
                .Select(x => x.Category);
        }
    }
}
