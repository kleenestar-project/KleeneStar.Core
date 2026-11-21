using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Workspaces
{
    /// <summary>
    /// Provides a selection of workspaces available for inheritance.
    /// </summary>
    [Title("Workspace inheritance selection")]
    [Cache]
    public sealed class Inherited : RestApiSelection<Workspace>
    {
        /// <summary>
        /// Applies the specified filter criteria to the given query object.
        /// </summary>
        protected override IQuery<Workspace> Filter(string filter, IQuery<Workspace> query, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(filter) || filter == "null")
            {
                return query;
            }

            return query.WhereContainsIgnoreCase(x => x.Name, filter);
        }

        /// <summary>
        /// Retrieves a queryable collection of workspaces.
        /// </summary>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<Workspace> query, IQueryContext context, IRequest request)
        {
            var workspaces = CoreHub.WorkspaceManager.GetWorkspaces(global::KleeneStar.Core.WebPermission.ContentVisibility.Restrict(query), context);

            var list = new List<RestApiSelectionItem>
            {
                new() { Id = Guid.Empty, Text = I18N.Translate(request, "kleenestar.core:workspace.property.none") }
            };

            list.AddRange(workspaces.Select(x => new RestApiSelectionItem()
            {
                Id = x.Id,
                Text = x.Name
            }));

            return list.AsQueryable();
        }
    }
}
