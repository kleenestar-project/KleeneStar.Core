using KleeneStar.Core.WebIdentity;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Api._1_.Identities
{
    /// <summary>
    /// Represents a REST API table for managing identity entities.
    /// </summary>
    [Title("kleenestar.core:setting.identity.table.header")]
    [Cache]
    public sealed class Table : KleeneStarRestApiTable<Model.Entities.Identity>
    {
        private readonly IUri _editFormUri;
        private readonly IUri _cloneFormUri;
        private readonly IUri _deleteFormUri;
        private readonly IUri _passwordFormUri;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Table()
        {
            _editFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Settings.Identity._identityid_.Edit>();
            _cloneFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Settings.Identity._identityid_.Clone>();
            _deleteFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Settings.Identity._identityid_.Delete>();
            _passwordFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Settings.Identity._identityid_.Password>();
        }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the collection of columns.
        /// </summary>
        protected override IEnumerable<RestApiTableColumn> RetrieveDefaultColumns(IRequest request)
        {
            yield return new RestApiTableColumn()
            {
                Id = "name",
                Label = I18N.Translate(request, "kleenestar.core:setting.identity.name.label"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "email",
                Label = I18N.Translate(request, "kleenestar.core:setting.identity.email.label"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "state",
                Label = I18N.Translate(request, "kleenestar.core:setting.identity.state.label"),
                Visible = false
            };

            yield return new RestApiTableColumn()
            {
                Id = "source",
                Label = I18N.Translate(request, "kleenestar.core:setting.identity.source.label"),
                Visible = true
            };
        }

        /// <summary>
        /// Retrieves table rows matching the query.
        /// </summary>
        protected override IEnumerable<RestApiTableRow> RetrieveRows(IQuery<Model.Entities.Identity> query, IQueryContext context, IEnumerable<RestApiTableColumn> columns, IRequest request)
        {
            return CoreHub.IdentityManager.GetIdentities(query, context)
                .Select(x => new RestApiTableRow
                {
                    Id = x.Id.ToString(),
                    Cells =
                    [
                        new RestApiTableCell() {
                             Content = x.Name
                        },
                        new() {
                            Content = x.Email
                        },
                        new() {
                            Content = x.State.ToString()
                        },
                        new() {
                            Content = SourceName(x, request)
                        }
                    ],
                    Options = GetOptions(x, request).Select(o => o.ToJson()),
                    Uri = GetUri(x, request)?.ToString()
                });
        }

        /// <summary>
        /// Applies filters to the query.
        /// </summary>
        protected override IQuery<Model.Entities.Identity> Filter(string filter, IQuery<Model.Entities.Identity> query, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(filter) || filter == "null")
            {
                return query;
            }

            return query.WhereContainsIgnoreCase
            (
                x => x.Name, filter
            );
        }

        /// <summary>
        /// Applies quick filters to the query.
        /// </summary>
        protected override IQuery<Model.Entities.Identity> Filter(IEnumerable<string> filters, IQuery<Model.Entities.Identity> query, IRequest request)
        {
            foreach (var filter in filters.Where(f => f.StartsWith("qf_", StringComparison.OrdinalIgnoreCase)))
            {
                var key = filter[3..];

                switch (key.ToLowerInvariant())
                {
                    case "active":
                        query = query.Where(x => x.State == IdentityState.Active);
                        break;
                    default:
                        continue;
                }
            }

            return query;
        }

        /// <summary>
        /// Retrieves options for a row.
        /// </summary>
        private IEnumerable<RestApiOption> GetOptions(Model.Entities.Identity row, IRequest request)
        {
            var editUri = _editFormUri?
                .BindParameters(request)
                .BindParameters(new IdentityIdParameter(row.Id));
            var cloneUri = _cloneFormUri?
                .BindParameters(request)
                .BindParameters(new IdentityIdParameter(row.Id));
            var deleteUri = _deleteFormUri?
                .BindParameters(request)
                .BindParameters(new IdentityIdParameter(row.Id));

            yield return new RestApiOptionHeader(request)
            {
                Text = "webexpress.webapp:header.setting.label"
            };

            yield return new RestApiOptionEdit(request)
            {
                PrimaryAction = new ActionModal("modal-form", editUri, TypeModalSize.ExtraLarge)
            };

            yield return new RestApiOptionClone(request)
            {
                PrimaryAction = new ActionModal("modal-form", cloneUri, TypeModalSize.ExtraLarge)
            };

            // a reset link is offered only where it can set something: an internal account
            if (AuthenticationSourceCatalog.ManagesPassword(row))
            {
                var passwordUri = _passwordFormUri?
                    .BindParameters(request)
                    .BindParameters(new IdentityIdParameter(row.Id));

                yield return new RestApiOptionCustom(request)
                {
                    Text = I18N.Translate(request, "kleenestar.core:setting.identity.password.title"),
                    Icon = new IconKey(),
                    PrimaryAction = new ActionModal("modal-form", passwordUri, TypeModalSize.Default)
                };
            }

            yield return new RestApiOptionSeparator(request);
            yield return new RestApiOptionDelete(request)
            {
                PrimaryAction = new ActionModal("modal-form", deleteUri, TypeModalSize.Small)
            };
        }

        /// <summary>
        /// Names the source that authenticates an account, or its stored key when the source
        /// is not installed.
        /// </summary>
        /// <param name="row">The account.</param>
        /// <param name="request">The request whose language the name is given in.</param>
        /// <returns>The name.</returns>
        private static string SourceName(Model.Entities.Identity row, IRequest request)
        {
            var source = AuthenticationSourceCatalog.Resolve(row);

            return source is null
                ? row.AuthenticationSource
                : I18N.Translate(request, source.Name);
        }

        /// <summary>
        /// Retrieves the URI for a row.
        /// </summary>
        private static IUri GetUri(Model.Entities.Identity row, IRequest request)
        {
            return null;
        }
    }
}
