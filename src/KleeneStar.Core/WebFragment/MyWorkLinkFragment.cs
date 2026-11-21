using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebFragment
{
    /// <summary>
    /// The way from the avatar menu to the signed-in person's own work across every workspace.
    /// </summary>
    [Section<SectionAppAvatarPrimary>]
    [Scope<IScopeGeneral>]
    [Scope<IScopeAdmin>]
    [Condition<global::WebExpress.WebApp.WebCondition.ConditionLogin>]
    [Cache]
    public sealed class MyWorkLinkFragment : FragmentControlDropdownItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        public MyWorkLinkFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.core:planning.mywork.title";
            Icon = _ => new IconUserClock();
            Uri = _ => CoreHub.GetUri<global::KleeneStar.Core.WWW.MyWork.Index>();
        }
    }
}
