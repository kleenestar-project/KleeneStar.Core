using KleeneStar.Model;
using System;
using System.Collections.Generic;
using System.Net;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;
using PasswordResetEntity = KleeneStar.Model.Entities.PasswordReset;

namespace KleeneStar.Core.WWW.Api._1_.Password
{
    /// <summary>
    /// Sets a password through a one-time reset link.
    /// </summary>
    /// <remarks>
    /// The caller is nobody yet - they are setting the password they will sign in with - so the
    /// endpoint asks for no identity; the link's secret is the authorization, and it names the
    /// account. It is spent by the first password it sets. A guess is pointless (the secret is
    /// 256 random bits), so there is no lockout to keep.
    /// </remarks>
    [Cache]
    public sealed class Reset : RestApiCrud<PasswordResetEntity>
    {
        /// <summary>
        /// The field that carries the secret from the link.
        /// </summary>
        public const string TokenField = "Token";

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
        public Reset()
        {
        }

        /// <summary>
        /// Refuses every change of a link.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.PUT)]
        [Method(RequestMethod.PATCH)]
        public override IResponse Update(IRequest request)
        {
            return new ResponseForbidden();
        }

        /// <summary>
        /// Refuses every removal of a link.
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
        /// Retrieves nothing: the stored links are hashes and are not read back.
        /// </summary>
        protected override IEnumerable<PasswordResetEntity> Retrieve(IQuery<PasswordResetEntity> query, IQueryContext context, IRequest request)
        {
            return [];
        }

        /// <summary>
        /// Validates the link and the new password.
        /// </summary>
        protected override IRestApiValidationResult Validate(PasswordResetEntity existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var result = base.Validate(existingItem, payload, request);
            var reset = CoreHub.CredentialManager.GetPasswordReset(Profile.Password.Read(payload, TokenField));

            if (reset is null)
            {
                result.Add(I18N.Translate(request, "kleenestar.core:password.reset.validation.link"), NewField);

                return result;
            }

            var account = CoreHub.IdentityManager.GetIdentity(reset.IdentityId);
            var next = Profile.Password.Read(payload, NewField);

            foreach (var error in CoreHub.CredentialManager.ValidatePassword(account, next))
            {
                result.Add(I18N.Translate(request, error), NewField);
            }

            if (!string.Equals(next, Profile.Password.Read(payload, ConfirmField), StringComparison.Ordinal))
            {
                result.Add(I18N.Translate(request, "kleenestar.core:password.validation.mismatch"), ConfirmField);
            }

            return result;
        }

        /// <summary>
        /// Sets the password and spends the link.
        /// </summary>
        protected override IRestApiCrudResultCreate Create(RestApiCrudFormData fieldMap, IRequest request, out PasswordResetEntity newItem)
        {
            newItem = null;

            var result = CoreHub.CredentialManager.CompletePasswordReset
            (
                Profile.Password.Read(fieldMap, TokenField),
                Profile.Password.Read(fieldMap, NewField)
            );

            if (!result.Succeeded)
            {
                return null;
            }

            var home = CoreHub.GetUri<global::KleeneStar.Core.WWW.Index>()?.ToString() ?? "/";
            var done = I18N.Translate(request, "kleenestar.core:password.reset.done");
            var signIn = I18N.Translate(request, "kleenestar.core:password.reset.signin");

            return new RestApiCrudResultCreate
            {
                Message = $"<p>{WebUtility.HtmlEncode(done)}</p><a href=\"{WebUtility.HtmlEncode(home)}\">{WebUtility.HtmlEncode(signIn)}</a>"
            };
        }
    }
}
