using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Issue._objectkey_
{
    /// <summary>
    /// Modal page that hosts the version history of an object: the commit chain on the left, the
    /// selected commit with its changed fields and its full replayed state on the right. The
    /// <c>{objectkey}</c> URL segment is declared by the sibling <see cref="Index"/> page, so
    /// this page carries no segment attribute of its own.
    /// </summary>
    /// <remarks>
    /// The route doubles as the deep link the concept's sitemap names: opening it directly serves
    /// the same content as a full page, which is what makes the dialog's content addressable
    /// rather than reachable only through the actions menu. The content itself is contributed by
    /// the scoped history fragment.
    /// <para>
    /// The page is not restricted to objects of the issue kind — like the sibling
    /// <see cref="Permission"/> page it is addressed by object key alone, so the actions menu of
    /// every kind can open it.
    /// </para>
    /// </remarks>
    [WebIcon<IconClockRotateLeft>]
    [Title("kleenestar.core:object.history.title")]
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class History : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public History()
        {
        }

        /// <summary>
        /// Processing of the resource. The content is contributed entirely by the scoped history
        /// fragment, so no work is required here.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
        }
    }
}
