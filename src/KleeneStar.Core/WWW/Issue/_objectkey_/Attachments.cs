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
    /// The files attached to an object, as a page of their own. The URL is
    /// <c>/issue/{objectkey}/attachments</c>; the <c>{objectkey}</c> segment is declared by the
    /// sibling <see cref="Index"/> page, so this page carries no segment attribute.
    /// </summary>
    /// <remarks>
    /// Like the sibling <see cref="History"/> and <see cref="Permission"/> pages this one is
    /// <b>not</b> restricted to the issue kind: it is addressed by object key alone, so the
    /// document and blog surfaces reach it too. That is what it exists for. An issue detail
    /// keeps its attachments on the page beside the work item, where they are part of the
    /// record; a document is prose, and a file list under the text would interrupt the reading
    /// rather than support it - so on those kinds the files move here, reached from the toolbar
    /// and the actions menu.
    /// </remarks>
    [WebIcon<IconPaperClip>]
    [Title("kleenestar.core:object.attachment.card.header")]
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class Attachments : IPage<VisualTreeWebApp>, IScope
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="objectManager">The object manager used to resolve the addressed object
        /// for the headline.</param>
        public Attachments(IObjectManager objectManager)
        {
            _objectManager = objectManager;
        }

        /// <summary>
        /// Processing of the resource. The file surface itself is contributed by the scoped
        /// attachment fragment; the page only names the object the files belong to.
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
