using KleeneStar.Core.WebIdentity;
using KleeneStar.Core.WebManager;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Profile
{
    /// <summary>
    /// Changes the password of the calling identity, which proves itself by the current one.
    /// </summary>
    /// <remarks>
    /// Confined to the caller like the rest of the profile: whatever id the form sends, it is
    /// the caller's password that changes. The endpoint reads nothing back but the caller's
    /// id - no field of the account travels to the password form, and no password ever
    /// travels back. An external account is refused: its password lives with its source.
    /// </remarks>
    [Cache]
    public sealed class Password : RestApiCrud<Identity>
    {
        /// <summary>
        /// The field that carries the current password.
        /// </summary>
        public const string CurrentField = "CurrentPassword";

        /// <summary>
        /// The field that carries the new password.
        /// </summary>
        public const string NewField = "NewPassword";

        /// <summary>
        /// The field that repeats the new password.
        /// </summary>
        public const string ConfirmField = "ConfirmPassword";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Password()
        {
        }

        /// <summary>
        /// Refuses a create: an account is not created through its profile.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.POST)]
        public override IResponse Create(IRequest request)
        {
            return new ResponseForbidden();
        }

        /// <summary>
        /// Refuses a delete: a password is replaced, never removed, through the profile.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.DELETE)]
        public override IResponse Delete(IRequest request)
        {
            return new ResponseForbidden();
        }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        /// <returns>An IQueryContext instance.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the caller - as an empty record carrying only its id, whatever the query
        /// names.
        /// </summary>
        protected override IEnumerable<Identity> Retrieve(IQuery<Identity> query, IQueryContext context, IRequest request)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            return identityId == Guid.Empty ? [] : [new Identity(identityId)];
        }

        /// <summary>
        /// Validates the three password fields.
        /// </summary>
        /// <remarks>
        /// Every rule is checked here, including the current password, so the form marks the
        /// field that is wrong instead of answering one sentence for all of them. The change
        /// itself checks again - this is the answer to the form, that is the guard.
        /// </remarks>
        protected override IRestApiValidationResult Validate(Identity existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var result = base.Validate(existingItem, payload, request);
            var account = existingItem is null ? null : CoreHub.IdentityManager.GetIdentity(existingItem.Id);

            if (account is null)
            {
                result.Add(I18N.Translate(request, "kleenestar.core:password.validation.signedout"), CurrentField);

                return result;
            }

            if (!AuthenticationSourceCatalog.ManagesPassword(account))
            {
                result.Add(I18N.Translate(request, "kleenestar.core:password.validation.external"), CurrentField);

                return result;
            }

            var current = Read(payload, CurrentField);
            var next = Read(payload, NewField);
            var confirm = Read(payload, ConfirmField);

            if (!IdentityPassword.IsUsable(account.PasswordHash)
                || !AuthenticationSourceCatalog.Resolve(account).VerifyPassword(account, current ?? string.Empty))
            {
                result.Add(I18N.Translate(request, "kleenestar.core:password.validation.current"), CurrentField);
            }

            foreach (var error in CoreHub.CredentialManager.ValidatePassword(account, next))
            {
                result.Add(I18N.Translate(request, error), NewField);
            }

            if (!string.Equals(next, confirm, StringComparison.Ordinal))
            {
                result.Add(I18N.Translate(request, "kleenestar.core:password.validation.mismatch"), ConfirmField);
            }

            return result;
        }

        /// <summary>
        /// Changes the password.
        /// </summary>
        protected override IRestApiCrudResultUpdate Update(Identity existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var result = CoreHub.CredentialManager.ChangePassword
            (
                existingItem.Id,
                Read(payload, CurrentField),
                Read(payload, NewField)
            );

            if (!result.Succeeded)
            {
                // validation passed a moment ago, so this is a race with another change; the
                // form reports a failed request rather than a success that did not happen
                throw new InvalidOperationException($"The password was not changed: {result.Outcome}.");
            }

            return new RestApiCrudResultUpdate
            {
                Message = I18N.Translate(request, "kleenestar.core:profile.security.password.changed")
            };
        }

        /// <summary>
        /// Reads a field out of the submitted payload, whichever spelling it arrived in.
        /// </summary>
        /// <param name="payload">The submitted form data.</param>
        /// <param name="field">The name of the field.</param>
        /// <returns>The value, or <see langword="null"/>.</returns>
        internal static string Read(RestApiCrudFormData payload, string field)
        {
            if (payload is null)
            {
                return null;
            }

            if (payload.TryGetValue(field.ToLowerInvariant(), out var lower))
            {
                return lower?.ToString();
            }

            return payload.TryGetValue(field, out var exact) ? exact?.ToString() : null;
        }
    }
}
