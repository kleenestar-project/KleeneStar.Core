using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebPolicies;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// The "recently opened" section of the issue overview / detail sidebar: the calling
    /// identity's most recently opened issues of the current workspace, below the flat
    /// kind links.
    /// </summary>
    [Section<SectionSidebarPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issues._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceViewPolicy>>]
    [Order(20)]
    [Cache]
    public sealed class IssueRecentSidebarFragment : ObjectRecentSidebarFragment
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services
        /// for its operation. Cannot be null.
        /// </param>
        /// <param name="objectManager">
        /// The object manager used to retrieve the recent issues. Cannot be null.
        /// </param>
        /// <param name="workspaceManager">
        /// The workspace manager used to resolve the workspace from the request. Cannot be null.
        /// </param>
        public IssueRecentSidebarFragment(IFragmentContext fragmentContext, IObjectManager objectManager, IWorkspaceManager workspaceManager)
            : base(fragmentContext, objectManager, workspaceManager)
        {
        }

        /// <summary>
        /// Gets the kind key listed by this section.
        /// </summary>
        protected override string Kind => Model.Entities.ObjectKind.Issue;
    }
}
