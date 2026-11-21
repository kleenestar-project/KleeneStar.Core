using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Issue._objectkey_
{
    /// <summary>
    /// What the unpublished draft of an object changes against its published text. The URL is
    /// <c>/issue/{objectkey}/draft</c>; the <c>{objectkey}</c> segment is declared by the
    /// sibling <see cref="Index"/> page, so this page carries no segment attribute.
    /// </summary>
    /// <remarks>
    /// Like the sibling <see cref="History"/> page it is addressed by object key alone and is
    /// not restricted to the issue kind - the prose editor of the document and blog kinds is
    /// what opens it, from the actions menu on its footer bar.
    /// <para>
    /// It answers the one question a draft raises that the editor itself cannot: the editor
    /// shows what the text <i>will</i> say, and the reading view shows what it says now, but
    /// neither shows the difference - and that difference is what publishing decides about.
    /// </para>
    /// </remarks>
    [WebIcon<IconCodeCompare>]
    [Title("kleenestar.core:object.draft.changes.title")]
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class Draft : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Draft()
        {
        }

        /// <summary>
        /// Processing of the resource. The comparison is contributed entirely by the scoped
        /// draft-changes fragment, so no work is required here.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
        }
    }
}
