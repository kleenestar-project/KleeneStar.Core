using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebInsight
{
    /// <summary>
    /// The dashboard: an insight made of columns of freely arranged widgets. It is the type the
    /// core ships, the one every former dashboard carries, and the one a create that names no
    /// type falls back to.
    /// </summary>
    public sealed class DashboardInsightType : IInsightType
    {
        /// <inheritdoc/>
        public string Key => Model.Entities.Insight.DashboardType;

        /// <inheritdoc/>
        public string Label => "kleenestar.core:insight.type.dashboard.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:insight.type.dashboard.description";

        /// <inheritdoc/>
        public IIcon Icon => new IconDashboard();

        /// <inheritdoc/>
        public int Order => 0;
    }
}
