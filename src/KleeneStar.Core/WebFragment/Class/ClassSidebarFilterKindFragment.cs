using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebPolicies;
using KleeneStar.Core.WebRestApi;
using System.Collections.Generic;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Class
{
    /// <summary>
    /// Represents the sidebar links that filter the class overview of a workspace by object
    /// type - one entry per registered kind, led by an entry that shows every class.
    /// </summary>
    /// <remarks>
    /// Modelled on the category filter of the workspace overview: the entries are filter
    /// actions of one exclusive group, so the table, tile and list views receive the picked
    /// kind through their filter binding and resolve it with <see cref="ClassKindFilter"/>.
    /// The entries follow <see cref="ObjectKindCatalog.Kinds"/>, so a kind an add-on registers
    /// is offered without this fragment knowing it.
    /// </remarks>
    [Section<SectionSidebarPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Classes._workspacekey_.Index>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceViewPolicy>>]
    [Order(10)]
    [Cache]
    public sealed class ClassSidebarFilterKindFragment : FragmentControlSidebarItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for its operation.
        /// Cannot be null.
        /// </param>
        public ClassSidebarFilterKindFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.core:class.quickfilter.all.label";
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
                new ControlSidebarItemLink("kind-all")
                {
                    Text = Text,
                    PrimaryAction = _ => new ActionFilterReset()
                    {
                        Exclusive = true,
                        Group = ClassKindFilter.FilterGroup
                    }
                }
                    .Render(renderContext, visualTree)
            };

            foreach (var kind in ObjectKindCatalog.Kinds)
            {
                list.Add(new ControlSidebarItemLink(ClassKindFilter.ToFilterId(kind.Key))
                {
                    Text = _ => kind.Label,
                    Icon = _ => kind.Icon,
                    PrimaryAction = _ => new ActionFilter()
                    {
                        Exclusive = true,
                        Group = ClassKindFilter.FilterGroup
                    }
                }
                    .Render(renderContext, visualTree));
            }

            return new HtmlList(list);
        }
    }
}
