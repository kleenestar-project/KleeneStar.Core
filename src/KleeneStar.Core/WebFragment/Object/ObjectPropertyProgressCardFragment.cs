using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using System.Globalization;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The progress an object reports for the objects it aggregates: a bar, the percentage, and
    /// how many of the counted objects are finished.
    /// </summary>
    /// <remarks>
    /// It renders only where the number means something - on an object that actually aggregates
    /// others. An object without children already says how far it has come through its state,
    /// and a bar repeating that state as a percentage would be a second, coarser copy of the
    /// status card above it.
    /// <para>
    /// The caption names the count rather than only the percentage, because the two answer
    /// different questions: 60 % says how far, <em>3 of 5</em> says how much is left and is the
    /// number somebody plans with.
    /// </para>
    /// </remarks>
    [Section<SectionPropertyPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Order(2)]
    [Cache]
    public sealed class ObjectPropertyProgressCardFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;
        private readonly IObjectProgressManager _progressManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the current object
        /// from the URL-bound object key.</param>
        /// <param name="progressManager">The progress manager that rolls the percentage up.</param>
        public ObjectPropertyProgressCardFragment
        (
            IFragmentContext fragmentContext,
            IObjectManager objectManager,
            IObjectProgressManager progressManager
        )
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _progressManager = progressManager;

            Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Three);
        }

        /// <summary>
        /// Renders the card, or nothing when the object aggregates no other object.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>An HTML node representing the rendered fragment, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var objectKey = renderContext?.Request?.GetParameter<ObjectKeyParameter>();
            var @object = _objectManager.GetObjectByKey(objectKey);

            if (@object is null)
            {
                return null;
            }

            var progress = _progressManager.GetProgress(@object.Id);

            if (!progress.Aggregated)
            {
                return null;
            }

            Clear();

            Add(new ControlText()
            {
                Text = _ => "kleenestar.core:object.progress.label",
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                Format = _ => TypeFormatText.Small
            });

            Add(new ControlProgress("object-progress")
            {
                Value = _ => (uint)progress.Percent,
                Format = _ => TypeFormatProgress.Striped,
                Color = _ => new PropertyColorProgress(progress.Percent >= 100 ? TypeColorProgress.Success : TypeColorProgress.Primary),
                Text = _ => progress.Percent.ToString(CultureInfo.InvariantCulture) + " %"
            });

            Add(new ControlText()
            {
                Text = _ => string.Format
                (
                    I18N.Translate(renderContext, "kleenestar.core:object.progress.contributions"),
                    progress.Completed,
                    progress.Contributions.Count
                ),
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                Format = _ => TypeFormatText.Small
            });

            return base.Render(renderContext, visualTree);
        }
    }
}
