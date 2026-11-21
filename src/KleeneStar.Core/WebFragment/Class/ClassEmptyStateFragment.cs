using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebFragment.Class
{
    /// <summary>
    /// Empty-state placeholder rendered on the class overview when the workspace addressed by the
    /// route has no class yet. Uses the standard <see cref="ControlEmptyState"/> pattern from the
    /// WebExpress.WebUI framework so the page still communicates the available next step.
    /// </summary>
    /// <remarks>
    /// <see cref="ClassEmptyStateCondition"/> gates this fragment and its complement gates
    /// <see cref="ClassViewFragment"/>, so the page shows exactly one of the two.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Classes._workspacekey_.Index>]
    [Cache]
    [Condition<ClassEmptyStateCondition>]
    public sealed class ClassEmptyStateFragment : FragmentControlEmptyState
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment. Cannot be null.
        /// </param>
        public ClassEmptyStateFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Icon = _ => new IconInbox();
            Title = _ => "kleenestar.core:class.empty.title";
            Message = _ => "kleenestar.core:class.empty.message";
        }
    }
}
