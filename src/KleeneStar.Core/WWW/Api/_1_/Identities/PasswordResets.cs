using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Identities
{
    /// <summary>
    /// Issues the one-time links that set the password of an internal account.
    /// </summary>
    /// <remarks>
    /// There is no mail delivery, so the link is answered to the administrator who issued it -
    /// in the create response, the only time its secret can be read - and handed over by them.
    /// The endpoint only creates: a stored link is a hash, and there is nothing in it worth
    /// listing. Issuing is for the account administrators alone (<see cref="AccountAuthorization"/>),
    /// because a link for somebody's account <em>is</em> that account; an external account is
    /// refused, its password is not this installation's to set.
    /// </remarks>
    [Cache]
    public sealed class PasswordResets : RestApiCrud<PasswordReset>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public PasswordResets()
        {
        }

        /// <summary>
        /// Issues a link, once the caller administers the accounts.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.POST)]
        public override IResponse Create(IRequest request)
        {
            return AccountAuthorization.IsAdministrator(request)
                ? base.Create(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Answers the empty form of the dialog, and nothing else: the stored links are not read
        /// back.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.GET)]
        public override IResponse Retrieve(IRequest request)
        {
            return AccountAuthorization.IsAdministrator(request)
                ? base.Retrieve(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Refuses every change: a link is issued, used or superseded, never edited.
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
        /// Refuses every removal: issuing a new link spends the open one.
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
        protected override IEnumerable<PasswordReset> Retrieve(IQuery<PasswordReset> query, IQueryContext context, IRequest request)
        {
            return [];
        }

        /// <summary>
        /// Validates the account the link is to be issued for.
        /// </summary>
        protected override IRestApiValidationResult Validate(PasswordReset existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var result = base.Validate(existingItem, payload, request);
            var account = CoreHub.IdentityManager.GetIdentity(ReadIdentityId(payload));

            if (account is null || account.State != IdentityState.Active)
            {
                result.Add(I18N.Translate(request, "kleenestar.core:setting.identity.password.validation.unknown"), nameof(PasswordReset.IdentityId));
            }
            else if (!WebIdentity.AuthenticationSourceCatalog.ManagesPassword(account))
            {
                result.Add(I18N.Translate(request, "kleenestar.core:setting.identity.password.validation.external"), nameof(PasswordReset.IdentityId));
            }

            return result;
        }

        /// <summary>
        /// Issues the link and answers it - the one time its secret can be read.
        /// </summary>
        protected override IRestApiCrudResultCreate Create(RestApiCrudFormData fieldMap, IRequest request, out PasswordReset newItem)
        {
            newItem = null;

            var issuer = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var result = CoreHub.CredentialManager.IssuePasswordReset(ReadIdentityId(fieldMap), issuer, out var secret);

            if (!result.Succeeded)
            {
                return null;
            }

            newItem = result.Reset;

            return new RestApiCrudResultCreate
            {
                Message = Describe(request, secret, newItem)
            };
        }

        /// <summary>
        /// Composes what the dialog shows: the link, ready to copy, and when it stops working.
        /// </summary>
        /// <remarks>
        /// The form shows the message as markup, so everything in it is encoded - the link is
        /// built from the request's own address, which a client can say anything about.
        /// </remarks>
        /// <param name="request">The request.</param>
        /// <param name="secret">The secret.</param>
        /// <param name="reset">The stored link.</param>
        /// <returns>The markup.</returns>
        private static string Describe(IRequest request, string secret, PasswordReset reset)
        {
            var link = BuildLink(request, secret);
            var expires = reset.Expires.ToLocalTime().ToString("g", request?.Culture ?? CultureInfo.CurrentCulture);
            var intro = I18N.Translate(request, "kleenestar.core:setting.identity.password.issued", expires);

            return $"<p>{WebUtility.HtmlEncode(intro)}</p>"
                + $"<input type=\"text\" class=\"form-control\" readonly=\"readonly\" onfocus=\"this.select()\" value=\"{WebUtility.HtmlEncode(link)}\"/>";
        }

        /// <summary>
        /// Builds the absolute address of the reset page carrying the secret.
        /// </summary>
        /// <param name="request">The request whose origin the link points at.</param>
        /// <param name="secret">The secret.</param>
        /// <returns>The link.</returns>
        private static string BuildLink(IRequest request, string secret)
        {
            var page = CoreHub.GetUri<global::KleeneStar.Core.WWW.SetPassword.Index>()?.ToString() ?? string.Empty;

            // the page address is usually absolute already; only a relative one needs the origin
            // of the request in front of it
            if (!Uri.TryCreate(page, UriKind.Absolute, out _)
                && Uri.TryCreate(request?.Uri?.ToString(), UriKind.Absolute, out var current))
            {
                page = current.GetLeftPart(UriPartial.Authority) + page;
            }

            return $"{page}?token={Uri.EscapeDataString(secret)}";
        }

        /// <summary>
        /// Reads the account the payload names.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <returns>The account id, or <see cref="Guid.Empty"/>.</returns>
        private static Guid ReadIdentityId(RestApiCrudFormData payload)
        {
            var key = nameof(PasswordReset.IdentityId);
            var raw = payload is null
                ? null
                : payload.TryGetValue(key.ToLowerInvariant(), out var lower) ? lower : payload.TryGetValue(key, out var exact) ? exact : null;

            return Guid.TryParse(raw?.ToString(), out var id) ? id : Guid.Empty;
        }
    }
}
