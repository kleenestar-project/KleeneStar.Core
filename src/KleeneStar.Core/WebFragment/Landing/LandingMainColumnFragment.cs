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
    /// The wide column of the landing page: what the organization has published lately, the
    /// ways into the work, and the help.
    /// </summary>
    /// <remarks>
    /// The two columns are one fragment each rather than one per section, because a column is
    /// what the grid places - a section contributed on its own would be laid out beside the
    /// columns rather than inside one. The sections themselves stay separate classes
    /// (<c>Landing…Section</c>), so what a section shows and where a column puts it remain two
    /// different decisions.
    /// <para>
    /// News leads here: it is the part of the page that changes between two visits, and the
    /// wide column is where several entries can be read side by side. The pinned content, which
    /// changes rarely and is looked up rather than read, sits in the side column opposite.
    /// </para>
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Condition<global::KleeneStar.Core.WebIdentity.SignedInCondition>]
    [Scope<global::KleeneStar.Core.WWW.Index>]
    [Order(30)]
    public sealed class LandingMainColumnFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;
        private readonly IObjectTagManager _tagManager;
        private readonly IWorkspaceManager _workspaceManager;
        private readonly IShareManager _shareManager;
        private readonly IWatcherManager _watcherManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager.</param>
        /// <param name="tagManager">The tag manager holding the label rows.</param>
        /// <param name="workspaceManager">The workspace manager.</param>
        /// <param name="shareManager">The share manager.</param>
        /// <param name="watcherManager">The watcher manager.</param>
        public LandingMainColumnFragment
        (
            IFragmentContext fragmentContext,
            IObjectManager objectManager,
            IObjectTagManager tagManager,
            IWorkspaceManager workspaceManager,
            IShareManager shareManager,
            IWatcherManager watcherManager
        )
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _tagManager = tagManager;
            _workspaceManager = workspaceManager;
            _shareManager = shareManager;
            _watcherManager = watcherManager;
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
                LandingNewsSection.Build(_objectManager, renderContext, visualTree),
                LandingEntryPathSection.Build(_objectManager, _workspaceManager, _shareManager, _watcherManager, renderContext),
                LandingSupportSection.Build(_tagManager, _objectManager, renderContext, visualTree)
            );

            return column.Render(renderContext, visualTree);
        }
    }
}
