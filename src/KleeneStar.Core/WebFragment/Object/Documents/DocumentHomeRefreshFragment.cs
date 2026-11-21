using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object.Documents
{
    /// <summary>
    /// Names the home page in the document overview's head, so the page reloads after the home
    /// page's editor published (<c>Assets/js/objectrefresh.js</c>).
    /// </summary>
    /// <remarks>
    /// Counterpart of <see cref="ObjectRefreshFragment"/> on the overview. The reading view is
    /// rendered by the server and the editor only closes after publishing, so the page reloads.
    /// Renders nothing into the body.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Documents._workspacekey_.Index>]
    [Cache]
    public sealed class DocumentHomeRefreshFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        public DocumentHomeRefreshFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Writes the meta tag naming the home page; renders nothing.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>Always <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var home = DocumentHome.Resolve(renderContext?.Request);

            if (home is not null)
            {
                visualTree?.AddMeta(ObjectRefreshFragment.MetaName, home.Id.ToString());
            }

            return null;
        }
    }
}
