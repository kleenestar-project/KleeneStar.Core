using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Main-panel content of the document reading view for a class rendered as a spreadsheet:
    /// the published workbook as a read-only spreadsheet. The title is the page headline, so it
    /// is not repeated here.
    /// </summary>
    /// <remarks>
    /// It is the reading view of the spreadsheet renderer, gated on
    /// <see cref="SpreadsheetRendererCondition"/>; a document rendered as prose or as a form
    /// gets <see cref="ObjectProseReadFragment"/> or <see cref="ObjectFormReadFragment"/> on the
    /// same route instead. An unpublished draft does not reach it, exactly as with prose.
    /// <para>
    /// A body that is no workbook - the text of a document whose class was switched to this
    /// renderer - is not shown as a sheet it is not; the view says so instead, and the workbook
    /// dialog opens on an empty workbook.
    /// </para>
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Condition<SpreadsheetRendererCondition>]
    [Cache]
    public sealed class ObjectSpreadsheetReadFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the addressed
        /// object from the URL-bound object key.</param>
        public ObjectSpreadsheetReadFragment(IFragmentContext fragmentContext, IObjectManager objectManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
        }

        /// <summary>
        /// Renders the reading view. Returns <c>null</c> when the fragment's render
        /// conditions exclude it or when no object can be resolved from the request.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var keyParameter = renderContext?.Request?.GetParameter<ObjectKeyParameter>();
            var @object = _objectManager.GetObjectByKey(keyParameter?.Value);

            if (@object is null)
            {
                return null;
            }

            var id = @object.Id.ToString("N");

            var body = new ControlPanel("object-spreadsheet-" + id)
            {
                Classes = ["ks-object-spreadsheet"]
            };

            var empty = ProseText.IsEmpty(@object.Description);

            body.Add
            (
                ObjectSpreadsheetView.Create
                (
                    "object-spreadsheet-body-" + id,
                    @object.Description,
                    ObjectSpreadsheetView.PageHeight,
                    "kleenestar.core:object.kind.document.read.empty"
                )
                ?? new ControlText("object-spreadsheet-none-" + id)
                {
                    Text = _ => empty
                        ? "kleenestar.core:object.kind.document.read.empty"
                        : "kleenestar.core:object.renderer.spreadsheet.read.foreign",
                    Format = _ => TypeFormatText.Paragraph,
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
                }
            );

            return body.Render(renderContext, visualTree);
        }
    }
}
