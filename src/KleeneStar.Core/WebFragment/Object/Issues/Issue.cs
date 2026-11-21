using KleeneStar.Core.WebParameter;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// The built-in issue kind: work items such as incidents, problems, or tasks. The
    /// overview lists the most recently updated issues and offers quickfilters for
    /// starred issues and personal scopes. Issues are the default kind — every object
    /// predating the kind partition behaves like a work item.
    /// </summary>
    public sealed class Issue : IObjectKind
    {
        /// <summary>
        /// Gets the persisted kind key of issues.
        /// </summary>
        public string Key => Model.Entities.ObjectKind.Issue;

        /// <summary>
        /// Gets the internationalization key of the plural display name.
        /// </summary>
        public string Label => "kleenestar.core:object.kind.issues.label";

        /// <summary>
        /// Gets the icon representing issues.
        /// </summary>
        public IIcon Icon => new IconListCheck();

        /// <summary>
        /// Gets the display order; issues close the built-in kind listings.
        /// </summary>
        public int Order => 3;

        /// <summary>
        /// Gets the unbound route of the issue overview page (the issue list).
        /// </summary>
        public IUri OverviewUri => CoreHub.GetUri<global::KleeneStar.Core.WWW.Issues._workspacekey_.Index>();

        /// <summary>
        /// Returns the issue detail view bound to the supplied object key
        /// (<c>/issue/{objectkey}</c>).
        /// </summary>
        /// <param name="objectKey">The key of the issue to address.</param>
        /// <returns>The bound detail route.</returns>
        public IUri DetailUri(string objectKey) => CoreHub
            .GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>()?
            .BindParameters(new ObjectKeyParameter(objectKey));

        /// <summary>
        /// Returns <see langword="null"/>: issues are edited through a modal opened from
        /// the detail page rather than on a dedicated edit route.
        /// </summary>
        /// <param name="objectKey">The key of the issue to address (unused).</param>
        /// <returns>Always <see langword="null"/>.</returns>
        public IUri EditUri(string objectKey) => null;
    }
}
