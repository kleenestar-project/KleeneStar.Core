using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebPolicies;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.Object.Assets
{
    /// <summary>
    /// The "recently opened" section of the asset overview / detail sidebar: the calling
    /// identity's most recently opened assets of the current workspace, below the flat
    /// kind links.
    /// </summary>
    [Section<SectionSidebarPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Assets._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceViewPolicy>>]
    [Order(20)]
    [Cache]
    public sealed class AssetRecentSidebarFragment : ObjectRecentSidebarFragment
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services
        /// for its operation. Cannot be null.
        /// </param>
        /// <param name="objectManager">
        /// The object manager used to retrieve the recent assets. Cannot be null.
        /// </param>
        /// <param name="workspaceManager">
        /// The workspace manager used to resolve the workspace from the request. Cannot be null.
        /// </param>
        public AssetRecentSidebarFragment(IFragmentContext fragmentContext, IObjectManager objectManager, IWorkspaceManager workspaceManager)
            : base(fragmentContext, objectManager, workspaceManager)
        {
        }

        /// <summary>
        /// Gets the kind key listed by this section.
        /// </summary>
        protected override string Kind => Model.Entities.ObjectKind.Asset;
    }
}
