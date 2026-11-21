using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_
{
    /// <summary>
    /// Dashboard endpoint of the asset overview's classic view: a small KPI dashboard
    /// aggregating the workspace's assets. The dashboard logic lives in
    /// <see cref="global::KleeneStar.Core.WebRestApi.RestApiObjectKindDashboard"/>; this
    /// endpoint only scopes it to the asset kind. It is an independent sibling of the
    /// issue dashboard endpoint (not a subclass), so both keep their own route.
    /// </summary>
    [Title("kleenestar.core:object.view.dashboard.title")]
    [Cache]
    public sealed class Dashboard : global::KleeneStar.Core.WebRestApi.RestApiObjectKindDashboard
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Dashboard()
        {
        }

        /// <summary>
        /// Gets the object kind the dashboard aggregates: assets.
        /// </summary>
        protected override string Kind => Model.Entities.ObjectKind.Asset;
    }
}
