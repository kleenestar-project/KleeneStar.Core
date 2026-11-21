using KleeneStar.Core.WebManager;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Workspaces
{
    /// <summary>
    /// Represents a unique key used for workspace management, ensuring that 
    /// each workspace identifier is valid and not reserved.
    /// </summary>
    [Title("Workspace")]
    [Cache]
    public sealed partial class UniqueKey : RestApiUnique
    {
        /// <summary>
        /// Provides a compiled regular expression that matches strings containing only alphanumeric
        /// characters (letters
        /// and digits).
        /// </summary>
        /// <remarks>
        /// Upper case is matched as well, because the keys the product proposes are upper case -
        /// the seeded workspaces and every workspace template's <c>SuggestedKey</c>. While this
        /// expression was lower case only, the wizard reported its own suggestion as unavailable.
        /// It is the same shape <see cref="Index"/> enforces on create, so the advice given while
        /// the form is filled in and the gate the create passes through cannot disagree.
        /// </remarks>
        [GeneratedRegex(@"^[a-zA-Z0-9-]{1,10}$")]
        private static partial Regex KeyRegex();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public UniqueKey()
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
            if (WorkspaceManager.ReservedWorkspaceKeys.Contains(value?.Trim().ToLower()))
            {
                return false;
            }

            if (!KeyRegex().IsMatch(value))
            {
                return false;
            }

            var query = new Query<Workspace>()
                .WhereStartsWithIgnoreCase(x => x.Key, value);

            using var context = ModelHub.CreateDbContext();

            return CoreHub.WorkspaceManager?.GetWorkspaces(query, context)
                .Any() != true;
        }
    }
}
