using KleeneStar.Core.WebPolicies;
using System;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebFragment.Template
{
    /// <summary>
    /// The sidebar link of the template and hierarchy overviews that leads to the templates of
    /// the workspace; its badge counts them.
    /// </summary>
    [Section<SectionSidebarPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Templates._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Templates._workspacekey_.Hierarchies>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceViewPolicy>>]
    [Order(0)]
    [Cache]
    public sealed class TemplateSidebarLinkFragment : TemplateAreaSidebarLinkFragment
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public TemplateSidebarLinkFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Icon = _ => new IconTemplate();
            Text = _ => "kleenestar.core:template.link.label";
        }

        /// <summary>
        /// Returns the address of the template overview.
        /// </summary>
        /// <returns>The address.</returns>
        protected override IUri GetOverviewUri()
        {
            return CoreHub.GetUri<global::KleeneStar.Core.WWW.Templates._workspacekey_.Index>();
        }

        /// <summary>
        /// Counts the templates of the workspace, as the template table lists them.
        /// </summary>
        /// <param name="workspaceId">The workspace.</param>
        /// <returns>The count.</returns>
        protected override int Count(Guid workspaceId)
        {
            return CoreHub.TemplateManager
                .GetTemplates(new Query<Model.Entities.Template>().Where(x => x.Class.WorkspaceId == workspaceId))
                .Count();
        }
    }
}
