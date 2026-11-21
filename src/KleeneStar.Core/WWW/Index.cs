using KleeneStar.Core.WebIcon;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;

namespace KleeneStar.Core.WWW
{
    /// <summary>
    /// The landing page - the shared starting point of the installation. It is not a greeting
    /// and not a dashboard: it is usable without any preparation, shows what the organization
    /// currently holds, and names the ways into the work.
    /// </summary>
    /// <remarks>
    /// The page contributes nothing itself. Its head is a fragment
    /// (<c>LandingHeadFragment</c>) because it carries a date line above the greeting and two
    /// actions beside it, which the page headline does not express - so the headline stays
    /// unset and the framework leaves it out.
    /// <para>
    /// Everything below is contributed by the fragments in <c>WebFragment/Landing/</c>: the
    /// head (10), the key figures (20), the wide column (30) and the narrow one (40). The two
    /// columns are what the page grid places; the sections inside them are separate classes
    /// (<c>Landing…Section</c>). That split is what keeps the page replaceable - an
    /// experienced user who would rather start on a dashboard replaces the destination, and an
    /// add-on wanting a section of its own contributes to a column instead of editing this
    /// class.
    /// </para>
    /// </remarks>
    [WebIcon<KleeneStarIcon>]
    [Title("kleenestar.core:kleenestar.label")]
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
        }
    }
}
