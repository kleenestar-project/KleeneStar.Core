using KleeneStar.Model.Entities;
using System;
using System.Linq;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Answers whether the caller of an endpoint may administer the accounts of the
    /// installation - create, change and remove them, and issue the one-time links that set
    /// their passwords.
    /// </summary>
    /// <remarks>
    /// The permission model's chains start at a workspace, and an account belongs to none, so
    /// there is no grant to ask (see <see cref="Group.AdministratorsId"/>). The answer is the
    /// membership of the administrators group, read from the <em>stored</em> account: the
    /// signed token carries no groups. It fails closed - an anonymous caller, an inactive
    /// account and an installation without the group administer nothing - because the
    /// operations behind it hand over accounts: a reset link for somebody else's account is
    /// that account.
    /// </remarks>
    public static class AccountAuthorization
    {
        /// <summary>
        /// Determines whether the caller of the request administers the accounts.
        /// </summary>
        /// <param name="request">The request. May be null, which answers from the request in
        /// flight.</param>
        /// <returns><see langword="true"/> when the caller is an active member of the
        /// administrators group.</returns>
        public static bool IsAdministrator(IRequest request)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            return IsAdministrator(identityId);
        }

        /// <summary>
        /// Determines whether an account administers the accounts.
        /// </summary>
        /// <param name="identityId">The account.</param>
        /// <returns><see langword="true"/> when it is an active member of the administrators group.</returns>
        public static bool IsAdministrator(Guid identityId)
        {
            if (identityId == Guid.Empty)
            {
                return false;
            }

            var identity = CoreHub.IdentityManager.GetIdentity(identityId);

            return identity is not null
                && identity.State == IdentityState.Active
                && identity.GroupMemberships.Any(x => x.Group is not null
                    && x.Group.Id == Group.AdministratorsId
                    && x.Group.State == GroupState.Active);
        }
    }
}
