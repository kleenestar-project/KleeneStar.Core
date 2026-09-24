using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Workspaces._workspacekey_
{
    /// <summary>
    /// Provides a selection of valid workspaces available for inheritance.
    /// </summary>
    [Title("Workspace inheritance selection")]
    [Cache]
    public sealed class Inherited : RestApiSelection<Workspace>
    {
        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        /// <returns>
        /// An IQueryContext instance that can be used to execute queries.
        /// </returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

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
        /// Retrieves valid inheritance options while preventing cyclic selections.
        /// </summary>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<Workspace> query, IQueryContext context, IRequest request)
        {
            var key = request.GetParameter<WorkspaceKeyParameter>()?.Value;
            var current = CoreHub.WorkspaceManager.GetWorkspaceByKey(key);
            var all = CoreHub.WorkspaceManager.GetWorkspaces(global::KleeneStar.Core.WebPermission.ContentVisibility.Restrict(query), context).ToList();

            var invalidIds = new HashSet<Guid>();
            if (current != null)
            {
                invalidIds.Add(current.Id);
                CollectDescendantIds(current.Id, all, invalidIds);
            }

            var list = new List<RestApiSelectionItem>
            {
                new() { Id = Guid.Empty, Text = I18N.Translate(request, "kleenestar.core:workspace.property.none") }
            };

            list.AddRange(all
                .Where(x => !invalidIds.Contains(x.Id))
                .Select(x => new RestApiSelectionItem()
                {
                    Id = x.Id,
                    Text = x.Name
                }));

            return list.AsQueryable();
        }

        private static void CollectDescendantIds(Guid parentId, IEnumerable<Workspace> all, ISet<Guid> collector)
        {
            var children = all.Where(x => x.InheritedId == parentId).ToList();
            foreach (var child in children)
            {
                if (collector.Add(child.Id))
                {
                    CollectDescendantIds(child.Id, all, collector);
                }
            }
        }
    }
}
