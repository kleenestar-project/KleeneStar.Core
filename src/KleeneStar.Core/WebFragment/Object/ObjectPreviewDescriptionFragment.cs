using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The description of the reduced object view, rendered as prose instead of the inline
    /// rich-text editor the full reading view puts in its place.
    /// </summary>
    /// <remarks>
    /// The editor of <see cref="ObjectDescriptionCardFragment"/> is the same control in every
    /// pane it lands in: a toolbar, a wysiwyg surface and a save round-trip, sized for a full
    /// content column. In a detail pane beside a list it takes more room than the text it holds
    /// and invites an edit the reader did not come for. The reduced view keeps the text and
    /// drops the editing: the same stored markup, rendered read-only through the prose wrapper
    /// the document and blog views use.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Preview>]
    [Order(1)]
    [Cache]
    public sealed class ObjectPreviewDescriptionFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the current object
        /// from the URL-bound object key.</param>
        public ObjectPreviewDescriptionFragment(IFragmentContext fragmentContext, IObjectManager objectManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
        }

        /// <summary>
        /// Renders the description. Returns <c>null</c> when the fragment's render conditions
        /// exclude it or when no object can be resolved from the request.
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

            var section = new ControlSection("object-preview-description-section")
            {
                Header = _ => "kleenestar.core:object.description.card.header",
                HeaderIcon = _ => new IconAlignLeft(),
                Layout = _ => TypeLayoutSection.Rule
            };

            if (ProseText.IsEmpty(@object.Description))
            {
                section.Add(new ControlText("object-preview-description-empty-" + id)
                {
                    Text = _ => "kleenestar.core:object.preview.description.none",
                    Format = _ => TypeFormatText.Paragraph,
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
                });

                return section.Render(renderContext, visualTree);
            }

            // the description is stored as the editor's document, so it is handed to the
            // framework's reading view - the same way the document and blog reading views
            // render their prose; emitted as markup, the preview showed the serialization
            section.Add(new ControlContent("object-preview-description-body-" + id)
            {
                Content = _ => @object.Description,
                Format = _ => TypeFormatContent.RichText,
                Classes = ["wx-kleenestar-prose"]
            });

            return section.Render(renderContext, visualTree);
        }
    }
}
