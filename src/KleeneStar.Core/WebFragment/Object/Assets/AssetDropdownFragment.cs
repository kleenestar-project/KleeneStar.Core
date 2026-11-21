using KleeneStar.Core.WebControl;
using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object.Assets
{
    /// <summary>
    /// Contributes the assets dropdown to the application header. The dropdown lists the
    /// calling identity's most recently opened assets (analogous to the workspace
    /// dropdown) and offers a search box scoped to the asset kind. It is one of the
    /// per-kind dropdowns that replaced the former single object dropdown.
    /// </summary>
    [Section<SectionAppNavigationPreferences>]
    [Scope<IScopeGeneral>]
    [Scope<IScopeAdmin>]
    [Scope<IScopeStatusPage>]
    [Order(4)]
    [Cache]
    public sealed class AssetDropdownFragment : ObjectKindDropdownControl, IFragmentControl<ObjectKindDropdownControl>, IFragmentControlNavigationItem
    {
        /// <summary>
        /// Gets the context of the fragment.
        /// </summary>
        public IFragmentContext FragmentContext { get; private set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for its
        /// operation. Cannot be null.
        /// </param>
        public AssetDropdownFragment(IFragmentContext fragmentContext)
            : base
            (
                fragmentContext?.FragmentId?.ToString(),
                new Asset(),
                _ => CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Assets.Dropdown>().ToString()
            )
        {
            FragmentContext = fragmentContext;
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
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
