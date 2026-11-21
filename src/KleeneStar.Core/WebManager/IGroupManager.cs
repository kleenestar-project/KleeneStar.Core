using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing groups.
    /// </summary>
    public interface IGroupManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when a group is added.
        /// </summary>
        event EventHandler<Group> GroupAdded;

        /// <summary>
        /// An event that fires when a group is updated.
        /// </summary>
        event EventHandler<Group> GroupUpdated;

        /// <summary>
        /// An event that fires when a group is removed.
        /// </summary>
        event EventHandler<Group> GroupRemoved;

        /// <summary>
        /// Returns a group based on its id.
        /// </summary>
        Group GetGroup(Guid groupId);

        /// <summary>
        /// Returns a group based on its id parameter.
        /// </summary>
        Group GetGroup(GroupIdParameter groupId);

        /// <summary>
        /// Retrieves groups matching the query.
        /// </summary>
        IEnumerable<Group> GetGroups(IQuery<Group> query);

        /// <summary>
        /// Retrieves groups matching the query with context.
        /// </summary>
        IEnumerable<Group> GetGroups(IQuery<Group> query, IQueryContext context);

        /// <summary>
        /// Returns how many groups satisfy the supplied filter criteria without loading
        /// them - the figure behind a headline such as the landing page's team count.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the counted groups. Paging must be left off:
        /// a query carrying it counts the page, not the whole result.
        /// </param>
        /// <returns>The number of matching groups.</returns>
        int CountGroups(IQuery<Group> query);

        /// <summary>
        /// Adds a group.
        /// </summary>
        IGroupManager Add(Group groupEntity);

        /// <summary>
        /// Updates a group.
        /// </summary>
        IGroupManager Update(Group groupEntity);

        /// <summary>
        /// Removes a group.
        /// </summary>
        IGroupManager Remove(Guid groupId);
    }
}
