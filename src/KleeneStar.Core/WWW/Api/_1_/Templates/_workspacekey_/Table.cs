using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebControl;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Api._1_.Templates._workspacekey_
{
    /// <summary>
    /// Represents a REST API table for managing template entities, providing data retrieval
    /// and option generation functionality for template records.
    /// </summary>
    /// <remarks>
    /// The composed row source builds the template hierarchy through framework row models.
    /// Filtering, sorting, paging and stored column layouts remain owned by the table endpoint.
    /// </remarks>
    [Title("kleenestar.core:template.table.header")]
    [Cache]
    public sealed class Table : IRestApi
    {
        private readonly Rows _rows = new();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Table()
        {
        }

        /// <summary>
        /// Returns the template rows of the addressed workspace, nested along the composition
        /// hierarchy.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response carrying the table document.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            return _rows.Retrieve(request);
        }

        /// <summary>
        /// Persists the column layout the client submitted.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response of the underlying table endpoint.</returns>
        [Method(RequestMethod.POST)]
        [Method(RequestMethod.PUT)]
        public IResponse Configure(IRequest request)
        {
            return _rows.Configure(request);
        }


    }

    /// <summary>
    /// The row source of the template overview: an ordinary REST table whose response the
    /// enclosing endpoint nests before returning it.
    /// </summary>
    /// <remarks>
    /// It is a type of its own rather than a nested one because the framework resolves the table's
    /// title from its own <see cref="TitleAttribute"/>, and it is not sealed-public so the endpoint
    /// scan does not register it as a second route. The stored column layout is addressed by a
    /// fixed key so it stays with the overview rather than with this implementation detail.
    /// </remarks>
    [Title("kleenestar.core:template.table.header")]
    internal sealed class Rows : KleeneStarRestApiTable<Model.Entities.Template>
    {
        private readonly IUri _editFormUri;
        private readonly IUri _cloneFormUri;
        private readonly IUri _deleteFormUri;
        private readonly IUri _avatarFormUri;

        /// <summary>
        /// Gets the key the per-user column layout of the template overview is stored under.
        /// </summary>
        protected override string TableLayoutKey => typeof(Table).FullName;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Rows()
        {
            _editFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Template._templateid_.Edit>();
            _cloneFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Template._templateid_.Clone>();
            _deleteFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Template._templateid_.Delete>();
            _avatarFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Template._templateid_.Avatar>();
        }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the collection of columns for the specified REST API request.
        /// </summary>
        protected override IEnumerable<RestApiTableColumn> RetrieveDefaultColumns(IRequest request)
        {
            yield return new RestApiTableColumn()
            {
                Id = "name",
                Label = I18N.Translate(request, "kleenestar.core:template.table.column.name"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "description",
                Label = I18N.Translate(request, "kleenestar.core:template.table.column.description"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "class",
                Label = I18N.Translate(request, "kleenestar.core:template.table.column.class"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "category",
                Label = I18N.Translate(request, "kleenestar.core:template.table.column.category"),
                Visible = true
            };

            // the composition hierarchy is the row tree, so it needs no column of its own
            yield return new RestApiTableColumn()
            {
                Id = "state",
                Label = I18N.Translate(request, "kleenestar.core:template.table.column.state"),
                Visible = false
            };
        }

        /// <summary>
        /// Counts matching templates before nesting changes the number of root rows.
        /// </summary>
        /// <param name="query">The filtered query before paging.</param>
        /// <param name="context">The current data context.</param>
        /// <param name="request">The request identifying the workspace.</param>
        /// <returns>The total number of matching templates.</returns>
        protected override int RetrieveTotal(IQuery<Model.Entities.Template> query, IQueryContext context, IRequest request)
        {
            var key = request.GetParameter<WorkspaceKeyParameter>();
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(key?.Value);
            var id = workspace?.Id ?? Guid.Empty;
            return CoreHub.TemplateManager.GetTemplates(query.Where(item => item.Class.WorkspaceId == id), context).Count();
        }

        /// <summary>
        /// Retrieves a collection of table rows that match the specified query and context.
        /// </summary>
        protected override IEnumerable<RestApiTableRow> RetrieveRows(IQuery<Model.Entities.Template> query, IQueryContext context, IEnumerable<RestApiTableColumn> columns, IRequest request)
        {
            var key = request.GetParameter<WorkspaceKeyParameter>();
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(key?.Value);
            var id = workspace?.Id ?? Guid.Empty;

            // a template is bound to a class, and the class to a workspace — so the overview of
            // a workspace is the set of templates whose class lives in that workspace
            query = query.Where(x => x.Class.WorkspaceId == id);

            // the children of a composite template are created in the order it defines, so the
            // rows follow that order too unless the caller sorted by a column of their own
            if (string.IsNullOrWhiteSpace(request.GetParameter("o")?.Value))
            {
                query = query
                    .OrderByAsc(x => x.Order)
                    .OrderByAsc(x => x.Name);
            }

            var templates = CoreHub.TemplateManager.GetTemplates(query, context).ToList();
            var rows = templates.Select(x => new RestApiTableRow
                {
                    Id = x.Id.ToString(),
                    Cells =
                    [
                        new RestApiTableCell() {
                             Content = x.Name
                        },
                        new() {
                            Content = ProseText.ToPlainText(x.Description)
                        },
                        new() {
                            Content = x.Class?.Name
                        },
                        new() {
                            Content = x.Category
                        },
                        new() {
                            Content = x.State.ToString()
                        }
                    ],
                    Options = GetOptions(x, request).Select(o => o.ToJson()),
                    Uri = GetUri(x, request)?.ToString(),
                    Image = x.Icon?.Uri?.ToString()
                }).ToDictionary(row => row.Id.ToString());

            var roots = new List<RestApiTableRow>();
            foreach (var template in templates)
            {
                var row = rows[template.Id.ToString()];
                var parent = template.ParentId?.ToString();
                var visited = new HashSet<string> { row.Id.ToString() };
                var ancestor = template;
                while (ancestor.ParentId is Guid ancestorId && visited.Add(ancestorId.ToString()))
                {
                    ancestor = templates.FirstOrDefault(item => item.Id == ancestorId);
                    if (ancestor is null)
                    {
                        break;
                    }
                }
                if (parent is not null && rows.TryGetValue(parent, out var parentRow) && ancestor is null)
                {
                    parentRow.Children = (parentRow.Children ?? []).Append(row).ToList();
                }
                else if (parent is not null && rows.TryGetValue(parent, out parentRow) && ancestor?.ParentId is null)
                {
                    parentRow.Children = (parentRow.Children ?? []).Append(row).ToList();
                }
                else
                {
                    roots.Add(row);
                }
            }
            return roots;
        }

        /// <summary>
        /// Applies the specified filter criteria to the given query object.
        /// </summary>
        protected override IQuery<Model.Entities.Template> Filter(string filter, IQuery<Model.Entities.Template> query, IRequest request)
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
        /// Applies the specified quick filter criteria to the given query object.
        /// </summary>
        protected override IQuery<Model.Entities.Template> Filter(IEnumerable<string> filters, IQuery<Model.Entities.Template> query, IRequest request)
        {
            // the sidebar's category section sends the picked category as a filter id; the group
            // is exclusive, so at most one arrives and the criteria stay a single comparison
            foreach (var category in filters
                .Select(f => TemplateCategoryFilter.TryGetCategory(f, out var c) ? c : null)
                .Where(c => c is not null))
            {
                query = query.Where(x => x.Category == category);
            }

            // the quick filter's class dropdown sends the picked class the same way; it is a
            // single-choice dropdown, so at most one arrives
            foreach (var classId in filters
                .Select(f => TemplateClassFilter.TryGetClass(f, out var c) ? c : Guid.Empty)
                .Where(c => c != Guid.Empty))
            {
                query = query.WhereEquals(x => x.ClassId, classId);
            }

            foreach (var filter in filters.Where(f => f.StartsWith("qf_", StringComparison.OrdinalIgnoreCase)))
            {
                var key = filter[3..];

                switch (key.ToLowerInvariant())
                {
                    case "active":
                        query = query.Where(x => x.State == TemplateState.Active);
                        break;
                    default:
                        continue;
                }
            }

            return query;
        }

        /// <summary>
        /// Retrieves a collection of options for the row.
        /// </summary>
        private IEnumerable<RestApiOption> GetOptions(Model.Entities.Template row, IRequest request)
        {
            var editUri = _editFormUri?
                .BindParameters(request)
                .BindParameters(new TemplateIdParameter(row.Id));
            var cloneUri = _cloneFormUri?
                .BindParameters(request)
                .BindParameters(new TemplateIdParameter(row.Id));
            var avatarUri = _avatarFormUri?
                .BindParameters(request)
                .BindParameters(new TemplateIdParameter(row.Id));
            var deleteUri = _deleteFormUri?
                .BindParameters(request)
                .BindParameters(new TemplateIdParameter(row.Id));


            yield return new RestApiOptionHeader(request)
            {
                Text = "webexpress.webapp:header.setting.label"
            };

            yield return new RestApiOptionEdit(request)
            {
                Icon = new IconPen(),
                PrimaryAction = new ActionModal("modal-form", editUri, TypeModalSize.ExtraLarge)
            };

            yield return new RestApiOptionCustom(request)
            {
                Text = I18N.Translate(request, "kleenestar.core:template.icon.title"),
                Icon = new IconPencil(),
                PrimaryAction = new ActionModal("modal-form", avatarUri, TypeModalSize.Default)
            };

            yield return new RestApiOptionClone(request)
            {
                Icon = new IconClone(),
                PrimaryAction = new ActionModal("modal-form", cloneUri, TypeModalSize.ExtraLarge)
            };

            yield return new RestApiOptionSeparator(request);
            yield return new RestApiOptionDelete(request)
            {
                Icon = new IconTrash(),
                PrimaryAction = new ActionModal("modal-form", deleteUri, TypeModalSize.Small)
            };
        }

        /// <summary>
        /// Retrieves a URI that represents the specified template row. A template has no detail
        /// page of its own, so the row is not a link and its actions are reached through the
        /// row's option menu instead.
        /// </summary>
        private static IUri GetUri(Model.Entities.Template row, IRequest request)
        {
            return null;
        }
    }
}
