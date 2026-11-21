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
    /// What changing an object touches, as a page of its own. The URL is
    /// <c>/issue/{objectkey}/impact</c>; the <c>{objectkey}</c> segment is declared by the
    /// sibling <see cref="Index"/> page, so this page carries no segment attribute.
    /// </summary>
    /// <remarks>
    /// It is a page rather than a third tab of the relation surface, because it answers a
    /// different question from the two readings that surface offers. The list and the graph
    /// there show <em>the relations of this object</em> - one hop, by definition, since that is
    /// what an object holds. This page shows the <em>consequences</em> of a change, which run
    /// through objects that have no relation to the one in hand at all, and which follow only
    /// the relations that carry an effect. Putting the two in one switcher would let a reader
    /// take the second for a longer version of the first.
    /// <para>
    /// As with <see cref="Relations"/> and <see cref="Attachments"/>, the page is addressed by
    /// object key alone and serves every object kind.
    /// </para>
    /// </remarks>
    [WebIcon<IconShareNodes>]
    [Title("kleenestar.core:object.impact.title")]
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class Impact : IPage<VisualTreeWebApp>, IScope
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="objectManager">The object manager used to resolve the addressed object
        /// for the headline.</param>
        public Impact(IObjectManager objectManager)
        {
            _objectManager = objectManager;
        }

        /// <summary>
        /// Processing of the resource. The graph itself is contributed by the scoped impact
        /// fragment; the page only names the object the analysis starts from.
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
