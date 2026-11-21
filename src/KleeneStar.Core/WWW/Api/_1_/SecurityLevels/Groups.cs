using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using WebExpress.WebIndex.Wql;

namespace KleeneStar.Core.WWW.Api._1_.SecurityLevels
{
    /// <summary>
    /// Serves the groups a security level can clear.
    /// </summary>
    /// <remarks>
    /// The clearance of a level is a set of groups, so the form that edits it needs the groups
    /// as a selection. It is a selection over the group table rather than the permission dialog's
    /// own group endpoint because the value it submits is the id list the level stores, not a
    /// grant.
    /// </remarks>
    [Title("kleenestar.core:securitylevel.clearance.label")]
    [Cache]
    public sealed class Groups : RestApiSelection<Group>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Groups()
        {
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
        /// Returns how a WQL statement is turned into a query: translatable, because the
        /// query runs against the database.
        /// </summary>
        protected override WqlQueryMode QueryMode => WqlQueryMode.Translatable;

        /// <summary>
        /// Returns the groups, ordered by name.
        /// </summary>
        /// <param name="query">The query parameters. Cannot be null.</param>
        /// <param name="context">The context in which the query is executed. Cannot be null.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The selection items.</returns>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<Group> query, IQueryContext context, IRequest request)
        {
            return CoreHub.GroupManager
                .GetGroups(query)
                .Where(x => x.State == GroupState.Active)
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new RestApiSelectionItem()
                {
                    Id = x.Id,
                    Text = x.Name
                })
                .AsQueryable();
        }

        /// <summary>
        /// Applies the specified filter criteria to the given query object.
        /// </summary>
        /// <param name="filter">The filter expression to apply.</param>
        /// <param name="query">The query object to which the filter will be applied.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The filtered query.</returns>
        protected override IQuery<Group> Filter(string filter, IQuery<Group> query, IRequest request)
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
    }
}
