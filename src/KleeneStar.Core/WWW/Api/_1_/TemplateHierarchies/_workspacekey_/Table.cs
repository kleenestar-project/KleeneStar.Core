using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
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
using WebExpress.WebCore.WebUri;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;

using TemplateHierarchyEntity = KleeneStar.Model.Entities.TemplateHierarchy;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Api._1_.TemplateHierarchies._workspacekey_
{
    /// <summary>
    /// The table of the template hierarchies of a workspace, shown on the hierarchy overview
    /// (<c>/templates/{workspacekey}/hierarchies</c>).
    /// </summary>
    /// <remarks>
    /// Columns: name, description, root template (with the class where the root template serves
    /// several), the number of nodes, the category and the state. The search matches the name;
    /// the category of the sidebar and the quick filter <c>active</c> narrow the rows as they do
    /// for the templates. A click on a row opens the edit dialog; its menu edits and deletes.
    /// </remarks>
    [Title("kleenestar.core:template.hierarchy.table.header")]
    [Cache]
    public sealed class Table : KleeneStarRestApiTable<TemplateHierarchyEntity>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Table()
        {
        }

        /// <summary>
        /// Creates the query context.
        /// </summary>
        /// <returns>The context.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Returns the columns of the table.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The columns.</returns>
        protected override IEnumerable<RestApiTableColumn> RetrieveDefaultColumns(IRequest request)
        {
            yield return new RestApiTableColumn { Id = "name", Label = I18N.Translate(request, "kleenestar.core:template.table.column.name"), Visible = true };
            yield return new RestApiTableColumn { Id = "description", Label = I18N.Translate(request, "kleenestar.core:template.table.column.description"), Visible = true };
            yield return new RestApiTableColumn { Id = "root", Label = I18N.Translate(request, "kleenestar.core:template.hierarchy.table.column.root"), Visible = true };
            yield return new RestApiTableColumn { Id = "nodes", Label = I18N.Translate(request, "kleenestar.core:template.hierarchy.table.column.nodes"), Visible = true };
            yield return new RestApiTableColumn { Id = "category", Label = I18N.Translate(request, "kleenestar.core:template.table.column.category"), Visible = true, Template = new RestApiTableColumnTemplateTag() };
            yield return new RestApiTableColumn { Id = "state", Label = I18N.Translate(request, "kleenestar.core:template.table.column.state"), Visible = false };
        }

        /// <summary>
        /// Counts the matching hierarchies of the addressed workspace.
        /// </summary>
        /// <param name="query">The filtered query before paging.</param>
        /// <param name="context">The context.</param>
        /// <param name="request">The request naming the workspace.</param>
        /// <returns>The total.</returns>
        protected override int RetrieveTotal(IQuery<TemplateHierarchyEntity> query, IQueryContext context, IRequest request)
        {
            var id = Workspace(request);

            return CoreHub.TemplateHierarchyManager.GetHierarchies(query.Where(x => x.WorkspaceId == id), context).Count();
        }

        /// <summary>
        /// Returns the rows of the hierarchies of the addressed workspace.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="context">The context.</param>
        /// <param name="columns">The columns.</param>
        /// <param name="request">The request naming the workspace.</param>
        /// <returns>The rows.</returns>
        protected override IEnumerable<RestApiTableRow> RetrieveRows(IQuery<TemplateHierarchyEntity> query, IQueryContext context, IEnumerable<RestApiTableColumn> columns, IRequest request)
        {
            var id = Workspace(request);

            query = query.Where(x => x.WorkspaceId == id);

            if (string.IsNullOrWhiteSpace(request.GetParameter("o")?.Value))
            {
                query = query.OrderByAsc(x => x.Name);
            }

            var hierarchies = CoreHub.TemplateHierarchyManager.GetHierarchies(query, context).ToList();
            var nodes = CoreHub.TemplateHierarchyManager.GetNodes(hierarchies.Select(x => x.Id));
            var editUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.TemplateHierarchy._hierarchyid_.Edit>();
            var deleteUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.TemplateHierarchy._hierarchyid_.Delete>();

            foreach (var hierarchy in hierarchies)
            {
                var root = hierarchy.RootTemplate ?? CoreHub.TemplateManager.GetTemplate(hierarchy.RootTemplateId);
                var rootClass = hierarchy.RootClassId is Guid classId && classId != root?.ClassId
                    ? CoreHub.ClassManager.GetClass(classId)?.Name
                    : null;
                var count = nodes.TryGetValue(hierarchy.Id, out var list) ? list.Count : 0;
                var edit = editUri?.BindParameters(request).BindParameters(new TemplateHierarchyIdParameter(hierarchy.Id));
                var delete = deleteUri?.BindParameters(request).BindParameters(new TemplateHierarchyIdParameter(hierarchy.Id));

                yield return new RestApiTableRow
                {
                    Id = hierarchy.Id.ToString(),
                    Cells =
                    [
                        new RestApiTableCell { Content = hierarchy.Name },
                        new RestApiTableCell { Content = ProseText.ToPlainText(hierarchy.Description) },
                        new RestApiTableCell { Content = rootClass is null ? root?.Name : $"{root?.Name} ({rootClass})" },
                        new RestApiTableCell { Content = I18N.Translate(request, $"kleenestar.core:template.hierarchy.table.nodes.{(count == 1 ? "one" : "other")}", count) },
                        new RestApiTableCell { Content = TemplateCategories.Normalize(hierarchy.Category) },
                        new RestApiTableCell { Content = hierarchy.State.ToString() }
                    ],
                    Options = Options(edit, delete, request).Select(o => o.ToJson()),
                    // a click on the row opens the edit dialog, beside the entry of the menu
                    PrimaryAction = new ActionModal("modal-form", edit, TypeModalSize.ExtraLarge).ToJson(),
                    Image = hierarchy.Icon?.Uri?.ToString()
                };
            }
        }

        /// <summary>
        /// Narrows the rows by name.
        /// </summary>
        /// <param name="filter">The search text.</param>
        /// <param name="query">The query.</param>
        /// <param name="request">The request.</param>
        /// <returns>The narrowed query.</returns>
        protected override IQuery<TemplateHierarchyEntity> Filter(string filter, IQuery<TemplateHierarchyEntity> query, IRequest request)
        {
            return string.IsNullOrWhiteSpace(filter) || filter == "null"
                ? query
                : query.WhereContainsIgnoreCase(x => x.Name, filter);
        }

        /// <summary>
        /// Narrows the rows by the category of the sidebar and the quick filter <c>active</c>.
        /// </summary>
        /// <param name="filters">The filter ids.</param>
        /// <param name="query">The query.</param>
        /// <param name="request">The request.</param>
        /// <returns>The narrowed query.</returns>
        protected override IQuery<TemplateHierarchyEntity> Filter(IEnumerable<string> filters, IQuery<TemplateHierarchyEntity> query, IRequest request)
        {
            foreach (var category in filters
                .Select(f => TemplateCategoryFilter.TryGetCategory(f, out var c) ? c : null)
                .Where(c => c is not null))
            {
                var first = category + TemplateCategories.Separator;
                var last = TemplateCategories.Separator + category;
                var inner = TemplateCategories.Separator + category + TemplateCategories.Separator;

                query = query.Where(x => x.Category == category
                    || x.Category.StartsWith(first)
                    || x.Category.EndsWith(last)
                    || x.Category.Contains(inner));
            }

            // the class dropdown keeps the hierarchies that create an object of the class, as
            // root or as a node
            foreach (var classId in filters
                .Select(f => TemplateClassFilter.TryGetClass(f, out var c) ? c : Guid.Empty)
                .Where(c => c != Guid.Empty))
            {
                var matching = HierarchiesCreating(classId);

                query = query.Where(x => matching.Contains(x.Id));
            }

            if (filters.Any(f => string.Equals(f, "qf_active", StringComparison.OrdinalIgnoreCase)))
            {
                query = query.Where(x => x.State == TemplateState.Active);
            }

            return query;
        }

        /// <summary>
        /// Returns the hierarchies that create an object of a class, as root or as a node.
        /// </summary>
        /// <param name="classId">The class.</param>
        /// <returns>The hierarchy ids.</returns>
        private static List<Guid> HierarchiesCreating(Guid classId)
        {
            var hierarchies = CoreHub.TemplateHierarchyManager.GetHierarchies(new Query<TemplateHierarchyEntity>()).ToList();
            var nodes = CoreHub.TemplateHierarchyManager.GetNodes(hierarchies.Select(x => x.Id));
            var templates = new Dictionary<Guid, Model.Entities.Template>();

            Guid ClassOf(Guid templateId, Guid? chosen)
            {
                if (!templates.TryGetValue(templateId, out var template))
                {
                    template = CoreHub.TemplateManager.GetTemplate(templateId);
                    templates[templateId] = template;
                }

                return CoreHub.TemplateHierarchyManager.ResolveClassId(chosen, template);
            }

            return [.. hierarchies
                .Where(h => ClassOf(h.RootTemplateId, h.RootClassId) == classId
                    || (nodes.TryGetValue(h.Id, out var list) && list.Any(n => ClassOf(n.TemplateId, n.ClassId) == classId)))
                .Select(h => h.Id)];
        }

        /// <summary>
        /// Returns the options of a row.
        /// </summary>
        /// <param name="edit">The address of the edit dialog.</param>
        /// <param name="delete">The address of the delete dialog.</param>
        /// <param name="request">The request.</param>
        /// <returns>The options.</returns>
        private static IEnumerable<RestApiOption> Options(IUri edit, IUri delete, IRequest request)
        {
            yield return new RestApiOptionHeader(request)
            {
                Text = "webexpress.webapp:header.setting.label"
            };

            yield return new RestApiOptionEdit(request)
            {
                Icon = new IconPen(),
                PrimaryAction = new ActionModal("modal-form", edit, TypeModalSize.ExtraLarge)
            };

            yield return new RestApiOptionSeparator(request);
            yield return new RestApiOptionDelete(request)
            {
                Icon = new IconTrash(),
                PrimaryAction = new ActionModal("modal-form", delete, TypeModalSize.Small)
            };
        }

        /// <summary>
        /// Returns the workspace the request names.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The workspace id, or the empty guid.</returns>
        private static Guid Workspace(IRequest request)
        {
            var key = request.GetParameter<WorkspaceKeyParameter>();

            return CoreHub.WorkspaceManager.GetWorkspaceByKey(key?.Value)?.Id ?? Guid.Empty;
        }
    }
}
