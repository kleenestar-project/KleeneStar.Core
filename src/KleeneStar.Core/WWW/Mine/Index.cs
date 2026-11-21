using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Mine
{
    /// <summary>
    /// "My issues" - the issues assigned to the calling identity plus the ones they
    /// raised, newest change first. The first of the entry paths the landing page names.
    /// </summary>
    /// <remarks>
    /// The page contributes the headline and the introduction; the list itself is rendered
    /// by <see cref="WebFragment.Landing.LandingMineListFragment"/>.
    /// </remarks>
    [WebIcon<IconListCheck>]
    [Title("kleenestar.core:landing.mine.title")]
    [Scope<IScopeGeneral>]
    public sealed class Index : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:landing.mine.title";

            visualTree.Content.MainPanel.AddPrimary(new ControlText("landing-mine-description")
            {
                Text = _ => "kleenestar.core:landing.mine.description",
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                Format = _ => TypeFormatText.Paragraph,
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Three)
            });
        }
    }
}
