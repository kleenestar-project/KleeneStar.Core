using KleeneStar.Model;
using System.Linq;
using System.Text.RegularExpressions;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Templates
{
    /// <summary>
    /// Represents a unique template name within the system, providing functionality to validate
    /// and check the availability of template names.
    /// </summary>
    [Title("Templates")]
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

            var query = new Query<Model.Entities.Template>()
                .WhereEqualsIgnoreCase(x => x.Name, value);

            // the template being edited does not compete with its own name
            if (System.Guid.TryParse(request?.GetParameter("exclude")?.Value, out var exclude))
            {
                query = query.Where(x => x.Id != exclude);
            }

            using var context = ModelHub.CreateDbContext();

            return CoreHub.TemplateManager?.GetTemplates(query, context)
                .Any() != true;
        }
    }
}
