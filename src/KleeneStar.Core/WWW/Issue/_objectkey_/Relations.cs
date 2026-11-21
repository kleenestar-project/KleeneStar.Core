using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Issue._objectkey_
{
    /// <summary>
    /// The relations an object holds - to other objects and to addresses outside the
    /// installation - as a page of their own. The URL is
    /// <c>/issue/{objectkey}/relations</c>; the <c>{objectkey}</c> segment is declared by the
    /// sibling <see cref="Index"/> page, so this page carries no segment attribute.
    /// </summary>
    /// <remarks>
    /// As with <see cref="Attachments"/>, the page is addressed by object key alone and is not
    /// restricted to the issue kind. On a document or a post the relation surface is reached
    /// from the toolbar and the actions menu rather than shown under the text: what a page links
    /// to is a question a reader asks after reading, not a column of the article.
    /// </remarks>
    [WebIcon<IconLinks>]
    [Title("kleenestar.core:object.relations.card.header")]
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class Relations : IPage<VisualTreeWebApp>, IScope
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="objectManager">The object manager used to resolve the addressed object
        /// for the headline.</param>
        public Relations(IObjectManager objectManager)
        {
            _objectManager = objectManager;
        }

        /// <summary>
        /// Processing of the resource. The relation surface itself is contributed by the scoped
        /// relation fragment; the page only names the object the relations belong to.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var objectParameter = renderContext.Request.GetParameter<ObjectKeyParameter>();
            var @object = _objectManager.GetObjectByKey(objectParameter?.Value);

            visualTree.Title = @object?.Summary;
            visualTree.Content.MainPanel.Headline.Title = @object?.Summary;
        }
    }
}
