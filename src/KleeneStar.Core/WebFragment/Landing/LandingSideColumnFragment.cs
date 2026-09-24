using KleeneStar.Core.WebManager;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// The narrow column of the landing page: the content the organization keeps in sight, the
    /// help, what people worked on last, and the invitation to say what is missing.
    /// </summary>
    /// <remarks>
    /// Everything here is looked up rather than read, so every section is a short list of
    /// <see cref="LandingRow"/>s. The pinned content leads because it is what a newcomer is
    /// sent to look for - the org chart, the guidelines; the help follows (it used to take the
    /// width of the wide column in three narrow columns), then the activity, and the invitation
    /// closes the column.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Condition<global::KleeneStar.Core.WebIdentity.SignedInCondition>]
    [Scope<global::KleeneStar.Core.WWW.Index>]
    [Order(40)]
    public sealed class LandingSideColumnFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;
        private readonly IObjectTagManager _tagManager;
        private readonly IAuditManager _auditManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager.</param>
        /// <param name="tagManager">The tag manager holding the label rows.</param>
        /// <param name="auditManager">The audit manager the activity is read from.</param>
        public LandingSideColumnFragment
        (
            IFragmentContext fragmentContext,
            IObjectManager objectManager,
            IObjectTagManager tagManager,
            IAuditManager auditManager
        )
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _tagManager = tagManager;
            _auditManager = auditManager;
        }

        /// <summary>
        /// Renders the column. Returns <c>null</c> when the fragment's render conditions
        /// exclude it.
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

            var column = new ControlPanel("landing-col-side")
            {
                Direction = _ => TypeDirection.Vertical,
                Classes = ["ks-landing-col-side"]
            };

            column.Add
            (
                LandingPinnedSection.Build(_tagManager, _objectManager, renderContext),
                LandingSupportSection.Build(_tagManager, _objectManager),
                LandingActivitySection.Build(_auditManager, _objectManager, renderContext),
                LandingFeedbackSection.Build(renderContext, visualTree)
            );

            return column.Render(renderContext, visualTree);
        }
    }
}
