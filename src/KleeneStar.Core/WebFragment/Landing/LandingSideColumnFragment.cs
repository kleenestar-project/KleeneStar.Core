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
    /// The narrow column of the landing page: the content the organization keeps in sight, what
    /// happened last, and the invitation to say what is missing.
    /// </summary>
    /// <remarks>
    /// The pinned content leads the column because it is the part a newcomer is sent to look
    /// for - the org chart, the guidelines - and it stays at the top of the page next to the
    /// news rather than below them. Beneath it the activity list carries what changes by the
    /// minute, and the invitation closes the column.
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
                LandingPinnedSection.Build(_tagManager, _objectManager, renderContext, visualTree),
                LandingActivitySection.Build(_auditManager, renderContext, visualTree),
                LandingFeedbackSection.Build(renderContext, visualTree)
            );

            return column.Render(renderContext, visualTree);
        }
    }
}
