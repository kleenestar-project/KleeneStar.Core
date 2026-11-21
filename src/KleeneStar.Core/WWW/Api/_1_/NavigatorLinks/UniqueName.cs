using KleeneStar.Model;
using System.Linq;
using System.Text.RegularExpressions;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.NavigatorLinks
{
    /// <summary>
    /// Represents a unique navigator link name within the system, providing functionality to validate
    /// and check the availability of navigator link names.
    /// </summary>
    [Title("Navigator links")]
    [Cache]
    public sealed partial class UniqueName : RestApiUnique
    {
        /// <summary>
        /// Provides a regular expression that matches names consisting of 1 to 64 non-control
        /// Unicode characters.
        /// </summary>
        [GeneratedRegex(@"^[\P{C}]{1,64}$")]
        private static partial Regex NameRegex();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public UniqueName()
        {
        }

        /// <summary>
        /// Determines whether the specified value is available based on the provided request context.
        /// </summary>
        /// <param name="value">
        /// The value to check for availability.
        /// </param>
        /// <param name="request">
        /// The request context containing additional information for the availability check.
        /// </param>
        /// <returns>True if the specified value is available; otherwise, false.</returns>
        protected override bool CheckAvailable(string value, Request request)
        {
            if (!NameRegex().IsMatch(value))
            {
                return false;
            }

            var query = new Query<Model.Entities.NavigatorLink>()
                .WhereEqualsIgnoreCase(x => x.Name, value);

            using var context = ModelHub.CreateDbContext();

            return CoreHub.NavigatorLinkManager?.GetNavigatorLinks(query, context)
                .Any() != true;
        }
    }
}
