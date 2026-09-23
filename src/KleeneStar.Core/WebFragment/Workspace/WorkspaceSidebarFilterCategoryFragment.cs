using KleeneStar.Core.WebPolicies;
using KleeneStar.Model.Entities;
using System.Collections.Generic;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Workspace
{
    /// <summary>
    /// Represents a sidebar link fragment for quick filtering workspace categories within the workspace manager
    /// interface.
    /// </summary>
    [Section<SectionSidebarPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Workspaces.Index>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceViewPolicy>>]
    [Cache]
    public sealed class WorkspaceSidebarFilterCategoryFragment : FragmentControlSidebarItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for its operation. 
        /// Cannot be null.
        /// </param>
        public WorkspaceSidebarFilterCategoryFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.core:workspace.quickfilter.all.label";
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
            var list = new List<IHtmlNode>
            {
                new ControlSidebarItemLink($"cat-all")
                {
                    Text = Text,
                    PrimaryAction = _ => new ActionFilterReset()
                    {
                        Exclusive = true,
                        Group = "category"
                    }
                }
                    .Render(renderContext, visualTree)
            };

            foreach (var category in CoreHub.WorkspaceManager.GetCategories(new Query<Category>()))
            {
                list.Add(new ControlSidebarItemLink($"cat-{category.Id}")
                {
                    Text = _ => category.Name,
                    PrimaryAction = _ => new ActionFilter()
                    {
                        Exclusive = true,
                        Group = "category"
                    }
                }
                    .Render(renderContext, visualTree));
            }

            return new HtmlList(list);
        }
    }
}
