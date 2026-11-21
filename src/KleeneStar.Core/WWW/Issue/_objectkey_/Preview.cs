using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Issue._objectkey_
{
    /// <summary>
    /// The reduced reading view of an object: the detail side every
    /// <see cref="Core.WebControl.ListDetailControl"/> fetches for the row a user selects. The
    /// <c>{objectkey}</c> URL segment is declared by the sibling <see cref="Index"/> page, so
    /// this page carries no segment attribute of its own.
    /// </summary>
    /// <remarks>
    /// A detail frame embeds a page's main content region, so pointing it at the full reading
    /// view put that view's whole content column - the inline-editable field card, the
    /// rich-text description editor, the attachment list, the comment thread and its composer -
    /// into a pane a few hundred pixels wide, while the property column with the status and the
    /// people stayed behind entirely: it lives in <c>#wx-content-property</c>, a sibling of the
    /// region the frame takes. What a pane needs is close to the opposite selection, so it gets
    /// a view of its own with elements adapted to it - identity and assignment as read-only
    /// attributes, the description and the configured view fields as text, and a button that
    /// opens the full view for everything the pane leaves out.
    /// <para>
    /// The page is not restricted to objects of the issue kind - like the sibling
    /// <see cref="History"/> and <see cref="Permission"/> pages it is addressed by object key
    /// alone, so the list of every kind can address it. Opening it directly renders it as a
    /// normal page, which makes the reduced view a deep link of its own.
    /// </para>
    /// </remarks>
    [WebIcon<IconEye>]
    [Title("kleenestar.core:object.preview.title")]
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class Preview : IPage<VisualTreeWebApp>, IScope
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="objectManager">
        /// The object manager used to resolve the object addressed by the route.
        /// </param>
        public Preview(IObjectManager objectManager)
        {
            _objectManager = objectManager;
        }

        /// <summary>
        /// Processing of the resource. The headline carries the object's summary, so the pane
        /// names what it shows without an element of its own repeating it; the content is
        /// contributed by the scoped preview fragments.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var objectParameter = renderContext.Request.GetParameter<ObjectKeyParameter>();
            var @object = _objectManager.GetObjectByKey(objectParameter?.Value);

            if (@object is null)
            {
                // the fragments render nothing without an object, which would leave the pane
                // blank and a stale selection indistinguishable from a slow load
                visualTree.Content.MainPanel.AddPrimary(new ControlEmptyState()
                {
                    Icon = _ => new IconMagnifyingGlass(),
                    Title = _ => "kleenestar.core:object.preview.unknown.title",
                    Message = _ => "kleenestar.core:object.preview.unknown.message"
                });

                return;
            }

            visualTree.Title = @object.Summary;
            visualTree.Content.MainPanel.Headline.Title = @object.Summary;
        }
    }
}
