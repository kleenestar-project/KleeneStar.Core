using KleeneStar.Core.WebFragment.Search;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebPermissions;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Api._1_.SavedSearches
{
    // The entity type SavedSearch collides with the sibling WWW.SavedSearch namespace;
    // alias it (inside the namespace block) so the bare name binds to the entity.
    using SavedSearch = KleeneStar.Model.Entities.SavedSearch;

    /// <summary>
    /// REST table over the saved searches the calling identity may see - their own and those
    /// shared with them. Each row runs its query, and the row options edit, share or delete it
    /// as far as the caller may.
    /// Supports the <c>q</c> substring search (by name) and a <c>qf_starred</c> quickfilter.
    /// </summary>
    [Title("kleenestar.core:search.saved.table.header")]
    [Cache]
    public sealed class Table : KleeneStarRestApiTable<SavedSearch>
    {
        private readonly IUri _editFormUri;
        private readonly IUri _deleteFormUri;
        private readonly IUri _permissionFormUri;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Table()
        {
            _editFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.SavedSearch._savedsearchid_.Edit>();
            _deleteFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.SavedSearch._savedsearchid_.Delete>();
            _permissionFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.SavedSearch._savedsearchid_.Permission>();
        }

        /// <summary>
        /// Creates a new query context backed by the application database.
        /// </summary>
        /// <returns>An <see cref="IQueryContext"/> instance.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the default column definitions of the saved-search table.
        /// </summary>
        /// <param name="request">The triggering request.</param>
        /// <returns>The default columns.</returns>
        protected override IEnumerable<RestApiTableColumn> RetrieveDefaultColumns(IRequest request)
        {
            yield return new RestApiTableColumn()
            {
                Id = "name",
                Label = I18N.Translate(request, "kleenestar.core:search.saved.column.name"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "query",
                Label = I18N.Translate(request, "kleenestar.core:search.saved.column.query"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "starred",
                Label = I18N.Translate(request, "kleenestar.core:search.saved.column.starred"),
                Visible = false
            };
        }

        /// <summary>
        /// Retrieves the saved-search rows of the calling identity.
        /// </summary>
        /// <param name="query">The query that defines the criteria for selecting rows.</param>
        /// <param name="context">The context in which the query is executed.</param>
        /// <param name="columns">The columns to include in the result.</param>
        /// <param name="request">The request object.</param>
        /// <returns>The matching rows.</returns>
        protected override IEnumerable<RestApiTableRow> RetrieveRows(IQuery<SavedSearch> query, IQueryContext context, IEnumerable<RestApiTableColumn> columns, IRequest request)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            if (identityId == Guid.Empty)
            {
                return [];
            }

            var shared = CoreHub.PermissionManager
                .GetGrantedIds(PermissionScope.SavedSearch, identityId, typeof(SavedSearchReadPermission))
                .ToList();

            query = query
                .Where(x => x.State == SavedSearchState.Active && (x.OwnerId == identityId || shared.Contains(x.Id)));

            return CoreHub.SavedSearchManager.GetSavedSearches(query, context)
                .OrderByDescending(x => x.Starred)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new RestApiTableRow
                {
                    Id = x.Id.ToString(),
                    Cells =
                    [
                        new RestApiTableCell() { Content = (x.Starred && x.OwnerId == identityId ? "★ " : string.Empty) + x.Name },
                        new() { Content = x.Query },
                        new() { Content = x.Starred.ToString() }
                    ],
                    Options = GetOptions(x, request).Select(o => o.ToJson()),
                    Uri = SavedSearchRun.RunUri(x)?.ToString()
                });
        }

        /// <summary>
        /// Applies the substring (name) search filter.
        /// </summary>
        /// <param name="filter">The filter expression.</param>
        /// <param name="query">The query to filter.</param>
        /// <param name="request">The request.</param>
        /// <returns>The filtered query.</returns>
        protected override IQuery<SavedSearch> Filter(string filter, IQuery<SavedSearch> query, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(filter) || filter == "null")
            {
                return query;
            }

            return query.WhereContainsIgnoreCase(x => x.Name, filter);
        }

        /// <summary>
        /// Applies the quickfilter criteria (the starred chip).
        /// </summary>
        /// <param name="filters">The selected quickfilter ids.</param>
        /// <param name="query">The query to filter.</param>
        /// <param name="request">The request.</param>
        /// <returns>The filtered query.</returns>
        protected override IQuery<SavedSearch> Filter(IEnumerable<string> filters, IQuery<SavedSearch> query, IRequest request)
        {
            foreach (var filter in filters.Where(f => f.StartsWith("qf_", StringComparison.OrdinalIgnoreCase)))
            {
                switch (filter[3..].ToLowerInvariant())
                {
                    case "starred":
                        query = query.Where(x => x.Starred);
                        break;
                    default:
                        continue;
                }
            }

            return query;
        }

        /// <summary>
        /// Builds the per-row option menu (run / edit / permissions / delete), offering only
        /// what the caller may do with the row.
        /// </summary>
        /// <param name="row">The saved search.</param>
        /// <param name="request">The request.</param>
        /// <returns>The row options.</returns>
        private IEnumerable<RestApiOption> GetOptions(SavedSearch row, IRequest request)
        {
            var editUri = _editFormUri?.BindParameters(new SavedSearchIdParameter(row.Id));
            var deleteUri = _deleteFormUri?.BindParameters(new SavedSearchIdParameter(row.Id));
            var permissionUri = _permissionFormUri?.BindParameters(new SavedSearchIdParameter(row.Id));

            yield return new RestApiOptionHeader(request)
            {
                Text = "webexpress.webapp:header.setting.label"
            };

            yield return new RestApiOptionCustom(request)
            {
                Uri = SavedSearchRun.RunUri(row),
                Text = I18N.Translate(request, "kleenestar.core:search.saved.run.label"),
                Icon = new IconMagnifyingGlass()
            };

            if (SavedSearchAuthorization.MayUpdate(row, request))
            {
                yield return new RestApiOptionEdit(request)
                {
                    Icon = new IconPen(),
                    PrimaryAction = new ActionModal("modal-form", editUri, TypeModalSize.ExtraLarge)
                };
            }

            if (SavedSearchAuthorization.MayAdminister(row, request))
            {
                yield return new RestApiOptionCustom(request)
                {
                    // a payload string is not a control: nothing on the client resolves a key in
                    // it, so the label is translated here like the built-in options translate theirs
                    Text = I18N.Translate(request, "kleenestar.core:search.saved.permission.label"),
                    Icon = new IconUserShield(),
                    PrimaryAction = new ActionModal("modal-form", permissionUri, TypeModalSize.ExtraLarge)
                };
            }

            if (SavedSearchAuthorization.MayDelete(row, request))
            {
                yield return new RestApiOptionSeparator(request);
                yield return new RestApiOptionDelete(request)
                {
                    Icon = new IconTrash(),
                    PrimaryAction = new ActionModal("modal-form", deleteUri, TypeModalSize.Small)
                };
            }
        }
    }
}
