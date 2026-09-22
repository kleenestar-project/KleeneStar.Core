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
    /// The overview presents the composition hierarchy as a row tree, so a template that is part of
    /// another appears below it instead of naming its parent in a column. The rows are produced by
    /// the ordinary table endpoint — which is composed rather than inherited here, so its filtering,
    /// sorting, paging and stored column layout apply unchanged — and reshaped afterwards by
    /// <see cref="RestApiTableShim"/>, because the framework's row and cell models can express
    /// neither a tree nor a cell carrying markup.
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
            // the description is authored in the rich-text editor and therefore stored as markup;
            // the column shows it rendered, as the object pages do, instead of printing its tags
            return RestApiTableShim.Apply
            (
                _rows.Retrieve(request),
                RetrieveRowParents(request),
                ["description"]
            );
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

        /// <summary>
        /// Maps each template of the addressed workspace to its parent template, which is what the
        /// row nesting is built from.
        /// </summary>
        /// <param name="request">The request carrying the workspace key.</param>
        /// <returns>The row-to-parent map, keyed and valued by template id.</returns>
        private static IReadOnlyDictionary<string, string> RetrieveRowParents(IRequest request)
        {
            var key = request.GetParameter<WorkspaceKeyParameter>();
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(key?.Value);
            var id = workspace?.Id ?? Guid.Empty;

            var query = new Query<Model.Entities.Template>()
                .Where(x => x.Class.WorkspaceId == id && x.ParentId != null);

            return CoreHub.TemplateManager.GetTemplates(query)
                .ToDictionary(x => x.Id.ToString(), x => x.ParentId.Value.ToString());
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

            return CoreHub.TemplateManager.GetTemplates(query, context)
                .Select(x => new RestApiTableRow
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
                });
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
