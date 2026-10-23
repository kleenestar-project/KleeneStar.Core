using KleeneStar.Core.WebPermission;
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
using WebExpress.WebIndex.Wql;

namespace KleeneStar.Core.WWW.Api._1_.Workspaces
{
    /// <summary>
    /// Selection endpoint feeding the blueprint workspace a new workspace is set up from: the
    /// readable workspaces holding at least one blueprint class.
    /// </summary>
    [Title("kleenestar.core:workspace.blueprint.label")]
    [Cache]
    public sealed class Blueprints : RestApiSelection<Workspace>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Blueprints()
        {
        }

        /// <inheritdoc/>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <inheritdoc/>
        protected override WqlQueryMode QueryMode => WqlQueryMode.Translatable;

        /// <inheritdoc/>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<Workspace> query, IQueryContext context, IRequest request)
        {
            var list = new List<RestApiSelectionItem>
            {
                new() { Id = Guid.Empty, Text = I18N.Translate(request, "kleenestar.core:workspace.blueprint.none") }
            };

            var holding = CoreHub.ClassManager
                .GetClasses(new Query<Model.Entities.Class>())
                .Where(x => x.IsBlueprint && x.State == ClassState.Active)
                .Select(x => x.WorkspaceId)
                .ToHashSet();

            list.AddRange(CoreHub.WorkspaceManager
                .GetWorkspaces(query, context)
                .Where(x => holding.Contains(x.Id) && ContentVisibility.MayRead(x.Id))
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new RestApiSelectionItem { Id = x.Id, Text = x.Name }));

            return list.AsQueryable();
        }

        /// <inheritdoc/>
        protected override IQuery<Workspace> Filter(string filter, IQuery<Workspace> query, IRequest request)
        {
            return string.IsNullOrWhiteSpace(filter) || filter == "null"
                ? query
                : query.WhereContainsIgnoreCase(x => x.Name, filter);
        }
    }
}
