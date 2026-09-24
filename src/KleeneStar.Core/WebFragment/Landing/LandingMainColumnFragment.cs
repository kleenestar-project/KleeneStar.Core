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
    /// The wide column of the landing page: what the organization has published lately, then
    /// the reader's open work.
    /// </summary>
    /// <remarks>
    /// The two columns are one fragment each rather than one per section, because a column is
    /// what the grid places - a section contributed on its own would be laid out beside the
    /// columns rather than inside one. The sections themselves stay separate classes
    /// (<c>Landing…Section</c>), so what a section shows and where a column puts it remain two
    /// different decisions.
    /// <para>
    /// The news lead, in the form of the blog overview; the reader's own open work follows as a
    /// list. The entry paths that used to stand here (mine, organization, shared,
    /// watched) are gone - they repeated the sidebar links beside them - and the help moved to
    /// the side column, where things that are looked up rather than read belong.
    /// </para>
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Condition<global::KleeneStar.Core.WebIdentity.SignedInCondition>]
    [Scope<global::KleeneStar.Core.WWW.Index>]
    [Order(30)]
    public sealed class LandingMainColumnFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager.</param>
        public LandingMainColumnFragment(IFragmentContext fragmentContext, IObjectManager objectManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
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

            var column = new ControlPanel("landing-col-main")
            {
                Direction = _ => TypeDirection.Vertical,
                Classes = ["ks-landing-col-main"]
            };

            column.Add
            (
                LandingNewsSection.Build(renderContext),
                LandingWorkSection.Build(_objectManager, renderContext)
            );

            return column.Render(renderContext, visualTree);
        }
    }
}
