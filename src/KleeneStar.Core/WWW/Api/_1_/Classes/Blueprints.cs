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

namespace KleeneStar.Core.WWW.Api._1_.Classes
{
    /// <summary>
    /// Selection endpoint feeding the blueprint a class follows: the classes marked as
    /// blueprint, in the workspaces the caller may read, labelled with their workspace.
    /// </summary>
    [Title("kleenestar.core:class.blueprint.label")]
    [Cache]
    public sealed class Blueprints : RestApiSelection<Model.Entities.Class>
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
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<Model.Entities.Class> query, IQueryContext context, IRequest request)
        {
            var list = new List<RestApiSelectionItem>
            {
                new() { Id = Guid.Empty, Text = I18N.Translate(request, "kleenestar.core:class.blueprint.none") }
            };

            list.AddRange(CoreHub.ClassManager
                .GetClasses(query, context)
                .Where(x => x.IsBlueprint && x.BlueprintId is null && x.State == ClassState.Active)
                .Where(x => ContentVisibility.MayRead(x.WorkspaceId))
                .Select(x => (Class: x, Workspace: CoreHub.WorkspaceManager.GetWorkspace(x.WorkspaceId)))
                .OrderBy(x => x.Workspace?.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.Class.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new RestApiSelectionItem
                {
                    Id = x.Class.Id,
                    Text = x.Workspace is null ? x.Class.Name : $"{x.Workspace.Name} · {x.Class.Name}"
                }));

            return list.AsQueryable();
        }

        /// <inheritdoc/>
        protected override IQuery<Model.Entities.Class> Filter(string filter, IQuery<Model.Entities.Class> query, IRequest request)
        {
            return string.IsNullOrWhiteSpace(filter) || filter == "null"
                ? query
                : query.WhereContainsIgnoreCase(x => x.Name, filter);
        }
    }
}
