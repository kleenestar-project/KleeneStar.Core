using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Core.WebControl;
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

// the endpoints live in KleeneStar.Core.WWW.Api._1_.SecurityLevels, so the bare entity
// name would resolve to the namespace rather than to the type
using SecurityLevelEntity = KleeneStar.Model.Entities.SecurityLevel;

namespace KleeneStar.Core.WWW.Api._1_.SecurityLevels._classid_
{
    /// <summary>
    /// Represents a REST API table for the security levels of a class.
    /// </summary>
    [Title("kleenestar.core:securitylevel.table.header")]
    [Cache]
    public sealed class Table : KleeneStarRestApiTable<SecurityLevelEntity>
    {
        private readonly IUri _editFormUri;
        private readonly IUri _cloneFormUri;
        private readonly IUri _deleteFormUri;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Table()
        {
            _editFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.SecurityLevel._securitylevelid_.Edit>();
            _cloneFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.SecurityLevel._securitylevelid_.Clone>();
            _deleteFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.SecurityLevel._securitylevelid_.Delete>();
        }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        /// <returns>An IQueryContext instance that can be used to execute queries.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the collection of columns for the specified REST API request.
        /// </summary>
        /// <param name="request">The request for which to retrieve the table columns.</param>
        /// <returns>The columns associated with the specified request.</returns>
        protected override IEnumerable<RestApiTableColumn> RetrieveDefaultColumns(IRequest request)
        {
            yield return new RestApiTableColumn()
            {
                Id = "name",
                Label = I18N.Translate(request, "kleenestar.core:securitylevel.name.label"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "description",
                Label = I18N.Translate(request, "kleenestar.core:securitylevel.description.label"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "clearance",
                Label = I18N.Translate(request, "kleenestar.core:securitylevel.clearance.label"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "rank",
                Label = I18N.Translate(request, "kleenestar.core:securitylevel.rank.label"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "default",
                Label = I18N.Translate(request, "kleenestar.core:securitylevel.isdefault.label"),
                Visible = false
            };

            yield return new RestApiTableColumn()
            {
                Id = "usage",
                Label = I18N.Translate(request, "kleenestar.core:securitylevel.usage.label"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "state",
                Label = I18N.Translate(request, "kleenestar.core:securitylevel.state.label"),
                Visible = false
            };
        }

        /// <summary>
        /// Retrieves the table rows that match the specified query and context.
        /// </summary>
        /// <param name="query">The query that defines the criteria for selecting table rows.</param>
        /// <param name="context">The context in which the query is executed.</param>
        /// <param name="columns">The columns to include in the result set.</param>
        /// <param name="request">The request object containing metadata for the retrieval.</param>
        /// <returns>The matching table rows, which may be empty.</returns>
        protected override IEnumerable<RestApiTableRow> RetrieveRows(IQuery<SecurityLevelEntity> query, IQueryContext context, IEnumerable<RestApiTableColumn> columns, IRequest request)
        {
            var classId = request.GetParameter<ClassIdParameter>();
            var guid = Guid.TryParse(classId?.Value, out Guid id) ? id : Guid.Empty;

            query = query.WhereEquals(x => x.ClassId, guid);

            var groups = CoreHub.GroupManager
                .GetGroups(new Query<Group>())
                .ToDictionary(x => x.Id, x => x.Name);

            // how many records a level guards is what tells an administrator whether narrowing
            // its clearance is a small change or a large one, so it is read once for the whole
            // class rather than once per row
            var usage = CountUsage(guid);

            return CoreHub.SecurityLevelManager.GetSecurityLevels(query, context)
                .OrderBy(x => x.Rank)
                .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
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
                            Content = Clearance(x, groups, request)
                        },
                        new() {
                            Content = x.Rank.ToString()
                        },
                        new() {
                            Content = I18N.Translate(request, x.IsDefault ? "kleenestar.core:answer.yes" : "kleenestar.core:answer.no")
                        },
                        new() {
                            Content = (usage.TryGetValue(x.Id, out var count) ? count : 0).ToString()
                        },
                        new() {
                            Content = I18N.Translate(request, x.State.Text())
                        }
                    ],
                    Options = GetOptions(x, request).Select(o => o.ToJson()),
                    Uri = null
                });
        }

        /// <summary>
        /// Applies the specified filter criteria to the given query object.
        /// </summary>
        /// <param name="filter">The filter expression to apply.</param>
        /// <param name="query">The query object to which the filter will be applied.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The filtered query.</returns>
        protected override IQuery<SecurityLevelEntity> Filter(string filter, IQuery<SecurityLevelEntity> query, IRequest request)
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
        /// Applies the selected quick filters to the given query object.
        /// </summary>
        /// <param name="filters">The quickfilter identifiers that should be applied.</param>
        /// <param name="query">The query object to which the filter will be applied.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The filtered query.</returns>
        protected override IQuery<SecurityLevelEntity> Filter(IEnumerable<string> filters, IQuery<SecurityLevelEntity> query, IRequest request)
        {
            foreach (var filter in filters.Where(f => f.StartsWith("qf_", StringComparison.OrdinalIgnoreCase)))
            {
                var key = filter[3..];

                switch (key.ToLowerInvariant())
                {
                    case "active":
                        query = query.Where(x => x.State == SecurityLevelState.Active);
                        break;
                    case "closed":
                        // the clearance is a serialized column, so "names no group" is the empty
                        // json array or nothing at all rather than a count the store can compare
                        query = query.Where(x => x.PermittedGroupIds == null || x.PermittedGroupIds.Count == 0);
                        break;
                    default:
                        continue;
                }
            }

            return query;
        }

        /// <summary>
        /// Names the groups a level clears, or says that it clears nobody.
        /// </summary>
        /// <param name="level">The level.</param>
        /// <param name="groups">The known groups, by id.</param>
        /// <param name="request">The request, for the culture of the message.</param>
        /// <returns>The clearance as a readable list.</returns>
        private static string Clearance(SecurityLevelEntity level, IReadOnlyDictionary<Guid, string> groups, IRequest request)
        {
            var names = (level.PermittedGroupIds ?? [])
                .Select(x => groups.TryGetValue(x, out var name) ? name : null)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            return names.Count == 0
                ? I18N.Translate(request, "kleenestar.core:securitylevel.clearance.none")
                : string.Join(", ", names);
        }

        /// <summary>
        /// Counts the objects classified with each level of the class.
        /// </summary>
        /// <remarks>
        /// The count has to see every record, including the ones the administrator reading the
        /// table is not cleared for - a level guarding twenty records must not report three
        /// because that is all the reader may open.
        /// </remarks>
        /// <param name="classId">The class whose levels are counted.</param>
        /// <returns>The number of objects per level id.</returns>
        private static IReadOnlyDictionary<Guid, int> CountUsage(Guid classId)
        {
            using var unrestricted = CoreHub.SecurityLevelManager.BeginUnrestricted();

            return CoreHub.ObjectManager
                .GetObjects(new Query<Model.Entities.Object>()
                    .WhereEquals(x => x.ClassId, classId))
                .Where(x => x.SecurityLevelId.HasValue)
                .GroupBy(x => x.SecurityLevelId.Value)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        /// <summary>
        /// Retrieves the row menu of a security level.
        /// </summary>
        /// <param name="row">The row the options are retrieved for. Cannot be null.</param>
        /// <param name="request">The request. Cannot be null.</param>
        /// <returns>The options offered beside the row.</returns>
        private IEnumerable<RestApiOption> GetOptions(SecurityLevelEntity row, IRequest request)
        {
            var editUri = _editFormUri?
                .BindParameters(request)
                .BindParameters(new SecurityLevelIdParameter(row.Id));
            var cloneUri = _cloneFormUri?
                .BindParameters(request)
                .BindParameters(new SecurityLevelIdParameter(row.Id));
            var deleteUri = _deleteFormUri?
                .BindParameters(request)
                .BindParameters(new SecurityLevelIdParameter(row.Id));

            yield return new RestApiOptionHeader(request)
            {
                Text = "webexpress.webapp:header.setting.label"
            };

            yield return new RestApiOptionEdit(request)
            {
                Icon = new IconPen(),
                PrimaryAction = new ActionModal("modal-form", editUri, TypeModalSize.ExtraLarge)
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
    }
}
