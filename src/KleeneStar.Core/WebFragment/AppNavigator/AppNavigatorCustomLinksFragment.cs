using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebNavigator;
using System.Linq;
using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebScope;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.AppNavigator
{
    /// <summary>
    /// Contributes the additional links configured on the navigator link settings page to the primary
    /// area of the app navigator.
    /// </summary>
    /// <remarks>
    /// Like the applications fragment, this renders one entry per configured link and returns them as
    /// an <see cref="HtmlList"/>, because the number of links is only known at runtime.
    /// </remarks>
    [Section<SectionAppPrimary>]
    [Scope<IScopeGeneral>]
    [Scope<IScopeAdmin>]
    [Cache]
    public sealed class AppNavigatorCustomLinksFragment : FragmentControlDropdownItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        public AppNavigatorCustomLinksFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Convert the control to HTML.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>
        /// An HTML node containing one entry per configured link, or null when the fragment is not
        /// applicable for the current request.
        /// </returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var links = CoreHub.NavigatorLinkManager?.GetVisibleNavigatorLinks() ?? [];
            var html = new HtmlList();

            foreach (var link in links)
            {
                var item = new ControlDropdownItemLink($"{Id}-{link.Id}")
                {
                    Text = _ => link.Name,
                    // the control emits the tooltip verbatim, so it is resolved here
                    Tooltip = _ => ProseText.ToPlainText(I18N.Translate(renderContext, link.Description)),
                    Uri = _ => ResolveUri(link.Uri),
                    Icon = _ => link.Icon
                };

                var node = item.Render(renderContext, visualTree);

                if (node != null)
                {
                    html.Add(node);
                }
            }

            return html;
        }

        /// <summary>
        /// Resolves a configured address into a uri.
        /// </summary>
        /// <param name="address">The configured address.</param>
        /// <returns>The resolved uri, or null when no address is configured.</returns>
        private static IUri ResolveUri(string address)
        {
            var normalized = NavigatorLinkAddress.Normalize(address);

            return normalized is null ? null : new UriEndpoint(normalized);
        }
    }
}
