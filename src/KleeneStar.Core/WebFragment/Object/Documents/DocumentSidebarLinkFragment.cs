using KleeneStar.Core.WebPolicies;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.Object.Documents
{
    /// <summary>
    /// Sidebar link that leads to the document overview (the hierarchical page tree)
    /// of the current workspace. Rendered on every kind overview and on the object
    /// detail page so the kinds stay switchable from everywhere; on the document
    /// overview the <see cref="DocumentSidebarTreeFragment"/> additionally contributes
    /// the tree section below the kind links.
    /// </summary>
    [Section<SectionSidebarPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Documents._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blogs._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issues._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Assets._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Edit>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Edit>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceViewPolicy>>]
    [Condition<DocumentKindConfiguredCondition>]
    [Order(1)]
    [Cache]
    public sealed class DocumentSidebarLinkFragment : ObjectKindSidebarLinkFragment
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services
        /// for its operation. Cannot be null.
        /// </param>
        public DocumentSidebarLinkFragment(IFragmentContext fragmentContext)
            : base(fragmentContext, new Document())
        {
        }
    }
}
