using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment
{
    /// <summary>
    /// Tells the editor's KleeneStar add-ons (<c>Assets/js/editoraddons.js</c>) where to read
    /// what they show: the address of <c>/api/1/editor/objects</c> in
    /// <c>&lt;meta name="kleenestar.editor.objects"&gt;</c> and that of the news feed
    /// <c>/api/1/blogs/feed</c> in <c>&lt;meta name="kleenestar.editor.news"&gt;</c>.
    /// </summary>
    /// <remarks>
    /// The add-ons run wherever a document is edited or read - an object page, a dialog, a
    /// preview pane - and the application's base path is not something a script can know.
    /// The meta reaches the head although the fragment renders in the body, the same way the
    /// session deadline does (<see cref="SessionMetaFragmentBase"/>); the fragment itself
    /// renders nothing.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<IScopeGeneral>]
    [Scope<IScopeAdmin>]
    [Cache]
    public sealed class EditorAddonMetaFragment : FragmentControlPanel
    {
        /// <summary>
        /// The name of the meta element carrying the address.
        /// </summary>
        public const string MetaName = "kleenestar.editor.objects";

        /// <summary>
        /// The name of the meta element carrying the address of the news feed.
        /// </summary>
        public const string NewsMetaName = "kleenestar.editor.news";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public EditorAddonMetaFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Writes the meta element and renders nothing.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>Always <see langword="null"/>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Editor.Objects>()?.ToString();

            if (!string.IsNullOrEmpty(uri))
            {
                visualTree?.AddMeta(MetaName, uri);
            }

            var news = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Blogs.Feed>()?.ToString();

            if (!string.IsNullOrEmpty(news))
            {
                visualTree?.AddMeta(NewsMetaName, news);
            }

            return null;
        }
    }
}
