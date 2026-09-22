using KleeneStar.Core.WebParameter;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebFragment.Object.Assets
{
    /// <summary>
    /// The built-in asset kind: configuration items such as hardware, software, or
    /// licenses. The overview lists the assets of a workspace, most recently updated
    /// first, with search, personal quickfilters, and pagination — mirroring the issue
    /// overview. Assets reuse the full object detail experience of the issue kind.
    /// </summary>
    public sealed class Asset : IObjectKind
    {
        /// <summary>
        /// Gets the persisted kind key of assets.
        /// </summary>
        public string Key => Model.Entities.ObjectKind.Asset;

        /// <summary>
        /// Gets the internationalization key of the plural display name.
        /// </summary>
        public string Label => "kleenestar.core:object.kind.assets.label";

        /// <summary>
        /// Gets the icon representing assets.
        /// </summary>
        public IIcon Icon => new IconCubes();

        /// <summary>
        /// Gets the display order; assets close the built-in kind listings.
        /// </summary>
        public int Order => 4;

        /// <summary>
        /// Gets <see langword="false"/>: an asset is inventory, not work - nothing waits on
        /// it, so no SLA times it.
        /// </summary>
        public bool ServiceLevels => false;

        /// <summary>
        /// Gets the unbound route of the asset overview page (the asset list).
        /// </summary>
        public IUri OverviewUri => CoreHub.GetUri<global::KleeneStar.Core.WWW.Assets._workspacekey_.Index>();

        /// <summary>
        /// Returns the asset detail view bound to the supplied object key
        /// (<c>/asset/{objectkey}</c>).
        /// </summary>
        /// <param name="objectKey">The key of the asset to address.</param>
        /// <returns>The bound detail route.</returns>
        public IUri DetailUri(string objectKey) => CoreHub
            .GetUri<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>()?
            .BindParameters(new ObjectKeyParameter(objectKey));

        /// <summary>
        /// Returns <see langword="null"/>: assets are edited through a modal opened from
        /// the detail page rather than on a dedicated edit route (mirroring the issue
        /// kind, whose action shells assets reuse).
        /// </summary>
        /// <param name="objectKey">The key of the asset to address (unused).</param>
        /// <returns>Always <see langword="null"/>.</returns>
        public IUri EditUri(string objectKey) => null;
    }
}
