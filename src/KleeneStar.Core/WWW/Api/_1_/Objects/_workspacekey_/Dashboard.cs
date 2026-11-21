using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_
{
    /// <summary>
    /// Dashboard endpoint of the issue overview's classic view: a small KPI dashboard
    /// aggregating the workspace's issues. The dashboard logic lives in
    /// <see cref="global::KleeneStar.Core.WebRestApi.RestApiObjectKindDashboard"/>; this
    /// endpoint only scopes it to the issue kind.
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
        /// Gets the object kind the dashboard aggregates: issues.
        /// </summary>
        protected override string Kind => Model.Entities.ObjectKind.Issue;
    }
}
