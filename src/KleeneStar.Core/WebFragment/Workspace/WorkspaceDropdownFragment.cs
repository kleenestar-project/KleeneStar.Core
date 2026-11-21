using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebPolicies;
using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Workspace
{
    /// <summary>
    /// Represents a dropdown fragment within a workspace, providing functionality to interact with and 
    /// manage dropdown options.
    /// </summary>
    [Section<SectionAppNavigationPreferences>]
    [Scope<IScopeGeneral>]
    [Scope<IScopeAdmin>]
    [Scope<IScopeStatusPage>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceViewPolicy>>]
    [Order(0)]
    [Cache]
    public sealed class WorkspaceDropdownFragment : WorkspaceDropdownControl, IFragmentControl<WorkspaceDropdownControl>, IFragmentControlNavigationItem
    {
        /// <summary>
        /// Gets the context of the fragment.
        /// </summary>
        public IFragmentContext FragmentContext { get; private set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for its operation. 
        /// Cannot be null.
        /// </param>
        public WorkspaceDropdownFragment(IFragmentContext fragmentContext)
            : base(fragmentContext?.FragmentId?.ToString())
        {
            FragmentContext = fragmentContext;
            Text = _ => "kleenestar.core:workspace.dropdown.label";
            // a glyph like the per-kind menus beside it (the overview page shows an image icon)
            Icon = _ => new WebExpress.WebUI.WebIcon.IconLayerGroup();
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
            // a caller who may neither read a workspace nor create one would open an empty menu
            if (!FragmentContext.Conditions.Check(renderContext?.Request)
                || !global::KleeneStar.Core.WebPermission.RouteAuthorization.MayListWorkspaces(renderContext?.Request))
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
