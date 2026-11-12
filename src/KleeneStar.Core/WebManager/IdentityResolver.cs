using KleeneStar.Model.Entities;
using System;
using System.Linq;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Finds an account by what a person writes down for it - an approval step's list of
    /// approvers, an automation rule's recipient or assignee.
    /// </summary>
    public static class IdentityResolver
    {
        /// <summary>
        /// Resolves an active account by user name, then e-mail address, then display name.
        /// </summary>
        /// <param name="text">What names the account.</param>
        /// <returns>The account, or <see langword="null"/>.</returns>
        public static Identity Resolve(string text)
        {
            var value = text?.Trim();

            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            var identities = CoreHub.IdentityManager
                .GetIdentities(new Query<Identity>())
                .Where(x => x.State == IdentityState.Active)
                .ToList();

            return identities.FirstOrDefault(x => string.Equals(x.UserName, value, StringComparison.OrdinalIgnoreCase))
                ?? identities.FirstOrDefault(x => string.Equals(x.Email, value, StringComparison.OrdinalIgnoreCase))
                ?? identities.FirstOrDefault(x => string.Equals(x.Name, value, StringComparison.OrdinalIgnoreCase));
        }
    }
}
