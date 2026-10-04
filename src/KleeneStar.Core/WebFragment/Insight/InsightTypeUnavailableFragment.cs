using KleeneStar.Core.WebInsight;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebFragment.Insight
{
    /// <summary>
    /// Stands in on the page of an insight whose type nobody registered - typically one whose
    /// plugin was uninstalled.
    /// </summary>
    /// <remarks>
    /// Each insight type draws its insights through a fragment of its own, gated on its type
    /// (<see cref="InsightDashboardFragment"/> for the dashboard). An insight whose type is gone
    /// has no such fragment, and the page would stand empty without a word; this notice says
    /// why. The insight itself is kept - it comes back the moment its type is registered again.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Insight._insightid_.Index>]
    [Condition<InsightUnavailableTypeCondition>]
    [Cache]
    public sealed class InsightTypeUnavailableFragment : FragmentControlEmptyState
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTypeUnavailableFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Icon = _ => new IconPuzzlePiece();
            Title = _ => "kleenestar.core:insight.type.unavailable.title";
            Message = _ => "kleenestar.core:insight.type.unavailable.message";
        }
    }
}
