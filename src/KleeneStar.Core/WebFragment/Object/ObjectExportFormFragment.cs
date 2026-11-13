using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebReport;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The export dialog of a single object: one tile selection
    /// (<see cref="ControlFormItemInputTile"/>) naming the file format.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tiles: the formats of <see cref="WebManager.IObjectExportManager"/> in their order (the core
    /// ships PDF, JSON and CSV; a plugin adds a <see cref="IObjectExportFormat"/>), each with
    /// icon, badge, description and footer naming the content; two columns, a search field and an
    /// empty text like the classification dialog. The first format is preselected
    /// (<see cref="PresetInputTile"/>).
    /// </para>
    /// <para>
    /// The form posts <c>Format</c> to <c>/api/1/exports/{objectkey}</c>, which answers the
    /// address of the file; <see cref="TypeRestFormSuccess.FollowUri"/> follows it, the browser
    /// downloads the attachment and the page stays. The form carries no item id, so
    /// <c>objectrefresh.js</c> does not reload the page.
    /// </para>
    /// </remarks>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Export>]
    [Cache]
    public sealed class ObjectExportFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// The name of the format input, matching the payload of the export endpoint.
        /// </summary>
        public const string FormatField = "Format";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectExportFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Exports._objectkey_.Index>();

            Submit.Text = _ => "kleenestar.core:object.export.submit.label";
            Submit.Icon = _ => new IconFileExport();
            SuccessAction = _ => TypeRestFormSuccess.FollowUri;
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control, or <c>null</c> for an unreadable object.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var @object = CoreHub.ObjectManager.GetObjectByKey(renderContext.Request.GetParameter<ObjectKeyParameter>());

            if (@object is null)
            {
                return null;
            }

            return base.Render(renderContext, visualTree, [CreateSelection(renderContext)]);
        }

        /// <summary>
        /// Creates the tile selection of the formats of <see cref="WebManager.IObjectExportManager"/>.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <returns>The selection.</returns>
        private static ControlFormItemInputTile CreateSelection(IRenderControlContext renderContext)
        {
            var formats = CoreHub.ObjectExportManager.Formats.ToList();
            var selection = new PresetInputTile(formats.FirstOrDefault()?.Key)
            {
                Name = _ => FormatField,
                Label = _ => "kleenestar.core:object.export.format.label",
                Help = _ => "kleenestar.core:object.export.format.help",
                Searchable = _ => true,
                SearchPlaceholder = _ => "kleenestar.core:object.export.format.search",
                EmptyText = _ => "kleenestar.core:object.export.format.empty",
                Columns = _ => 2,
                Required = _ => true
            };

            foreach (var format in formats)
            {
                selection.Add(CreateCard(renderContext, format));
            }

            return selection;
        }

        /// <summary>
        /// Creates the tile of a format.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="format">The format.</param>
        /// <returns>The tile.</returns>
        private static ControlTileCard CreateCard(IRenderControlContext renderContext, IObjectExportFormat format)
        {
            var card = new ControlTileCard(format.Key)
            {
                Header = _ => I18N.Translate(renderContext, format.Label),
                Icon = _ => format.Icon,
                Badge = _ => format.Badge,
                BadgeColor = _ => new PropertyColorTile(format.Color)
            };

            card.Add(new ControlText { Text = _ => I18N.Translate(renderContext, format.Description) });
            card.AddFooter(new ControlText { Text = _ => I18N.Translate(renderContext, format.Content) });

            return card;
        }
    }
}
