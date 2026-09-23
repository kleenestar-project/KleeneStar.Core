using KleeneStar.Core.WebIdentity;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Identities
{
    /// <summary>
    /// Provides CRUD operations for identity items via a REST API.
    /// </summary>
    /// <remarks>
    /// Reading is open - the people pickers and mentions read the directory - but never
    /// carries a credential, and writing is for the account administrators
    /// (<see cref="AccountAuthorization"/>): an account changed by anybody is an account taken
    /// over by anybody, since its e-mail address and its authentication source decide who can
    /// sign in as it.
    /// </remarks>
    [Cache]
    public sealed class Index : RestApiCrud<Model.Entities.Identity>
    {
        /// <summary>
        /// The payload keys that are never bound onto an account: the credentials, which have
        /// surfaces of their own (the profile's password form, the reset links), and the
        /// subject that links an account to its external source, which only a sign-in writes.
        /// </summary>
        private static readonly string[] CredentialFields =
        [
            nameof(Model.Entities.Identity.PasswordHash),
            nameof(Model.Entities.Identity.PasswordChanged),
            nameof(Model.Entities.Identity.ExternalSubject)
        ];

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Answers a read; the reads that prepare a change (the edit, clone and delete dialogs)
        /// only for an account administrator.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.GET)]
        public override IResponse Retrieve(IRequest request)
        {
            var preparesChange = request?.GetParameter("mode")?.Value is "edit" or "delete" or "clone" or "new";

            return !preparesChange || AccountAuthorization.IsAdministrator(request)
                ? base.Retrieve(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Creates or clones an account, once the caller administers the accounts.
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
        /// Changes an account, once the caller administers the accounts.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.PUT)]
        [Method(RequestMethod.PATCH)]
        public override IResponse Update(IRequest request)
        {
            return AccountAuthorization.IsAdministrator(request)
                ? base.Update(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Removes an account, once the caller administers the accounts.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.DELETE)]
        public override IResponse Delete(IRequest request)
        {
            return AccountAuthorization.IsAdministrator(request)
                ? base.Delete(request)
                : new ResponseForbidden();
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
        /// Retrieves identity items matching the query, with their credentials blanked.
        /// </summary>
        /// <remarks>
        /// The REST serializer ships every public property and does not honour
        /// <see cref="System.Text.Json.Serialization.JsonIgnoreAttribute"/>, so the hash on the
        /// entity reaches the client unless it is removed from the copy that is serialized.
        /// Saving such a copy does not write the blank back - the model's update leaves the
        /// credential columns alone.
        /// </remarks>
        protected override IEnumerable<Model.Entities.Identity> Retrieve(IQuery<Model.Entities.Identity> query, IQueryContext context, IRequest request)
        {
            return CoreHub.IdentityManager.GetIdentities(query, context)
                .Select(Sanitize);
        }

        /// <summary>
        /// Retrieves data for creating a new identity.
        /// </summary>
        protected override IRestApiCrudResultRetrieve RetrieveForCreate(IRequest request)
        {
            return base.RetrieveForCreate(request);
        }

        /// <summary>
        /// Retrieves data for cloning an identity.
        /// </summary>
        protected override IRestApiCrudResultRetrieve RetrieveForClone(IQuery<Model.Entities.Identity> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.IdentityManager.GetIdentities(query, context)
                .FirstOrDefault();

            // the copy is a new account: it takes the source, never a credential or the
            // subject that links the original to it
            var newItem = new Model.Entities.Identity()
            {
                Name = data.Name + " (Copy)",
                Email = data.Email,
                Avatar = data.Avatar,
                State = IdentityState.Active,
                AuthenticationSource = data.AuthenticationSource
            };

            return RetrieveForClone(request, newItem);
        }

        /// <summary>
        /// Retrieves data for updating an identity.
        /// </summary>
        protected override IRestApiCrudResultRetrieve RetrieveForUpdate(IQuery<Model.Entities.Identity> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.IdentityManager.GetIdentities(query, context)
                .FirstOrDefault();

            return RetrieveForUpdate(request, Sanitize(data));
        }

        /// <summary>
        /// Retrieves data for deleting an identity.
        /// </summary>
        protected override IRestApiCrudResultRetrieveDelete RetrieveForDelete(IQuery<Model.Entities.Identity> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.IdentityManager.GetIdentities(query, context)
                .FirstOrDefault();

            return RetrieveForDelete(request, Sanitize(data), data?.Id.ToString());
        }

        /// <summary>
        /// Validates the data for create or update operations.
        /// </summary>
        /// <remarks>
        /// An authentication source has to be one the installation knows: an account filed
        /// under a source that is not registered cannot sign in at all.
        /// </remarks>
        protected override IRestApiValidationResult Validate(Model.Entities.Identity existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var result = base.Validate(existingItem, payload, request);

            var (source, sent) = ReadField(payload, nameof(Model.Entities.Identity.AuthenticationSource));

            if (sent && AuthenticationSourceCatalog.GetSource(IdentitySource.FromId(FirstOf(source))) is null)
            {
                result.Add
                (
                    I18N.Translate(request, "kleenestar.core:setting.identity.source.validation.unknown"),
                    nameof(Model.Entities.Identity.AuthenticationSource)
                );
            }

            return result;
        }

        /// <summary>
        /// Creates a new identity.
        /// </summary>
        protected override IRestApiCrudResultCreate Create(RestApiCrudFormData fieldMap, IRequest request, out Model.Entities.Identity newItem)
        {
            var id = Guid.NewGuid();
            newItem = new Model.Entities.Identity(id)
            {
                Avatar = CoreHub.GenerateIcon(id),
                State = IdentityState.Active
            };

            WithoutCredentials(fieldMap).BindTo(newItem);

            CoreHub.IdentityManager.Add(newItem);

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Clones an existing identity.
        /// </summary>
        protected override IRestApiCrudResultCreate Clone(Model.Entities.Identity existingItem, RestApiCrudFormData fieldMap, IRequest request, out Model.Entities.Identity newItem)
        {
            var id = Guid.NewGuid();
            newItem = new Model.Entities.Identity(id)
            {
                Avatar = CoreHub.GenerateIcon(id),
                State = IdentityState.Active
            };

            WithoutCredentials(fieldMap).BindTo(newItem);

            CoreHub.IdentityManager.Add(newItem);

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Updates an existing identity.
        /// </summary>
        protected override IRestApiCrudResultUpdate Update(Model.Entities.Identity existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var res = base.Update(existingItem, WithoutCredentials(payload), request);

            CoreHub.IdentityManager.Update(existingItem);

            return res;
        }

        /// <summary>
        /// Deletes an identity.
        /// </summary>
        protected override IRestApiCrudResultDelete Delete(Model.Entities.Identity existingItem, IRequest request)
        {
            CoreHub.IdentityManager.Remove(existingItem.Id);

            return base.Delete(existingItem, request);
        }

        /// <summary>
        /// Blanks the credentials of an identity before it leaves the server.
        /// </summary>
        /// <param name="identity">The identity. May be null.</param>
        /// <returns>The same instance, without its password hash.</returns>
        private static Model.Entities.Identity Sanitize(Model.Entities.Identity identity)
        {
            if (identity is not null)
            {
                identity.PasswordHash = null;
            }

            return identity;
        }

        /// <summary>
        /// Removes the credential keys from a payload, in either spelling the payload may carry
        /// them in.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <returns>The same payload, without credentials.</returns>
        private static RestApiCrudFormData WithoutCredentials(RestApiCrudFormData payload)
        {
            foreach (var key in payload.Keys.Where(k => CredentialFields.Any(c => string.Equals(c, k, StringComparison.OrdinalIgnoreCase))).ToList())
            {
                payload.Remove(key);
            }

            return payload;
        }

        /// <summary>
        /// Returns the first entry of a value a selection submitted, which may be a semicolon
        /// separated list.
        /// </summary>
        /// <param name="value">The submitted value.</param>
        /// <returns>The first entry, trimmed, or <see langword="null"/>.</returns>
        private static string FirstOf(string value)
        {
            return value?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        }

        /// <summary>
        /// Reads a field out of the submitted payload, whichever spelling it arrived in.
        /// </summary>
        /// <param name="payload">The submitted form data.</param>
        /// <param name="field">The name of the field, as the property spells it.</param>
        /// <returns>The value, and whether the payload carried the field at all.</returns>
        private static (string Value, bool Sent) ReadField(RestApiCrudFormData payload, string field)
        {
            if (payload is null)
            {
                return (null, false);
            }

            if (payload.TryGetValue(field.ToLowerInvariant(), out var lower))
            {
                return (lower?.ToString(), true);
            }

            return payload.TryGetValue(field, out var exact) ? (exact?.ToString(), true) : (null, false);
        }
    }
}
