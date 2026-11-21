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
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WWW.Api._1_.Groups
{
    /// <summary>
    /// Represents a REST API table for managing group entities.
    /// </summary>
    [Title("kleenestar.core:setting.group.table.header")]
    [Cache]
    public sealed class Table : KleeneStarRestApiTable<Model.Entities.Group>
    {
        private readonly IUri _editFormUri;
        private readonly IUri _cloneFormUri;
        private readonly IUri _deleteFormUri;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Table()
        {
            _editFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Settings.Group._groupid_.Edit>();
            _cloneFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Settings.Group._groupid_.Clone>();
            _deleteFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Settings.Group._groupid_.Delete>();
        }

        /// <summary>
        /// Creates a query context.
        /// </summary>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves columns.
        /// </summary>
        protected override IEnumerable<RestApiTableColumn> RetrieveDefaultColumns(IRequest request)
        {
            yield return new RestApiTableColumn()
            {
                Id = "name",
                Label = I18N.Translate(request, "kleenestar.core:setting.group.name.label"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "description",
                Label = I18N.Translate(request, "kleenestar.core:setting.group.description.label"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "state",
                Label = I18N.Translate(request, "kleenestar.core:setting.group.state.label"),
                Visible = false
            };
        }

        /// <summary>
        /// Retrieves table rows.
        /// </summary>
        protected override IEnumerable<RestApiTableRow> RetrieveRows(IQuery<Model.Entities.Group> query, IQueryContext context, IEnumerable<RestApiTableColumn> columns, IRequest request)
        {
            return CoreHub.GroupManager.GetGroups(query, context)
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
                            Content = x.State.ToString()
                        }
                    ],
                    Options = GetOptions(x, request).Select(o => o.ToJson()),
                    Uri = GetUri(x, request)?.ToString()
                });
        }

        /// <summary>
        /// Applies text filter.
        /// </summary>
        protected override IQuery<Model.Entities.Group> Filter(string filter, IQuery<Model.Entities.Group> query, IRequest request)
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
        /// Applies quick filters.
        /// </summary>
        protected override IQuery<Model.Entities.Group> Filter(IEnumerable<string> filters, IQuery<Model.Entities.Group> query, IRequest request)
        {
            foreach (var filter in filters.Where(f => f.StartsWith("qf_", StringComparison.OrdinalIgnoreCase)))
            {
                var key = filter[3..];

                switch (key.ToLowerInvariant())
                {
                    case "active":
                        query = query.Where(x => x.State == GroupState.Active);
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
        private IEnumerable<RestApiOption> GetOptions(Model.Entities.Group row, IRequest request)
        {
            var editUri = _editFormUri?
                .BindParameters(request)
                .BindParameters(new GroupIdParameter(row.Id));
            var cloneUri = _cloneFormUri?
                .BindParameters(request)
                .BindParameters(new GroupIdParameter(row.Id));
            var deleteUri = _deleteFormUri?
                .BindParameters(request)
                .BindParameters(new GroupIdParameter(row.Id));

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

            yield return new RestApiOptionSeparator(request);
            yield return new RestApiOptionDelete(request)
            {
                PrimaryAction = new ActionModal("modal-form", deleteUri, TypeModalSize.Small)
            };
        }

        /// <summary>
        /// Retrieves the URI for a row.
        /// </summary>
        private static IUri GetUri(Model.Entities.Group row, IRequest request)
        {
            return null;
        }
    }
}
