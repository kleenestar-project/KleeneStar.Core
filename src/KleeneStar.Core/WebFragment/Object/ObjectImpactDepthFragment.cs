using KleeneStar.Core.WebParameter;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// How far the impact page looks: one button per offered depth, the current one filled in.
    /// </summary>
    /// <remarks>
    /// The chooser is buttons rather than a dropdown because there are five of them and picking
    /// one is the only thing to do on this page: a reader asks "and one step further?" several
    /// times in a row, and a menu makes each of those two clicks. Each entry is an ordinary link
    /// carrying the depth in the address, so the reading can be bookmarked and handed on, and so
    /// the page needs no state of its own.
    /// </remarks>
    [Section<SectionHeadlinePrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Impact>]
    [Cache]
    public sealed class ObjectImpactDepthFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectImpactDepthFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two);
        }

        /// <summary>
        /// Renders the chooser, or nothing when the request addresses no object.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>An HTML node representing the rendered fragment, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var objectKey = renderContext?.Request?.GetParameter<ObjectKeyParameter>();

            if (string.IsNullOrWhiteSpace(objectKey?.Value))
            {
                return null;
            }

            var current = ObjectImpactDepth.Resolve(renderContext);

            // the chooser is rebuilt per render rather than filled once in the constructor: the
            // fragment is cached and serves every object and every depth from one instance, so a
            // list built up front would mark the depth of whoever asked first
            Clear();
            Add(ObjectImpactDepth.Offered.Select(depth => Button(depth, current, renderContext)).ToArray<IControl>());

            return base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Builds the entry for one depth.
        /// </summary>
        /// <param name="depth">The depth the entry stands for.</param>
        /// <param name="current">The depth the page is currently showing.</param>
        /// <param name="renderContext">The render context, for binding the object of the page
        /// into the address.</param>
        /// <returns>The control.</returns>
        private static ControlButtonLink Button(int depth, int current, IRenderControlContext renderContext)
        {
            var label = depth.ToString(CultureInfo.InvariantCulture);

            return new ControlButtonLink($"impact-depth-{label}")
            {
                Text = _ => label,
                Tooltip = _ => "kleenestar.core:object.impact.depth.tooltip",
                Size = _ => TypeSizeButton.Small,
                BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Primary),

                // the current depth is the filled button and the others are outlines, which is
                // how a set of alternatives says which one is being looked at without a caption
                Outline = _ => depth != current,
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.One, PropertySpacing.Space.None, PropertySpacing.Space.None),
                Uri = context => CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.Impact>()?
                    .Add(new UriQuery(ObjectImpactDepth.Parameter, label))
                    .BindParameters(context?.Request ?? renderContext?.Request)
            };
        }
    }
}
