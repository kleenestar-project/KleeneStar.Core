using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages group entities within the application.
    /// </summary>
    public sealed class GroupManager : IGroupManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// An event that fires when a group is added.
        /// </summary>
        public event EventHandler<Group> GroupAdded;

        /// <summary>
        /// An event that fires when a group is updated.
        /// </summary>
        public event EventHandler<Group> GroupUpdated;

        /// <summary>
        /// An event that fires when a group is removed.
        /// </summary>
        public event EventHandler<Group> GroupRemoved;

        /// <summary>
        /// Returns the collection of names that are reserved.
        /// </summary>
        public static IEnumerable<string> ReservedGroupNames =>
        [
            "default", "admin", "system", "assets", "api", "group",
            "groups", "icons", "setting"
        ];

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private GroupManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns a group based on its id.
        /// </summary>
        public Group GetGroup(Guid groupId)
        {
            var query = new Query<Group>()
                .Where(x => x.Id == groupId)
                .WithPaging(0, 1);

            return ModelHub.GetGroups(query)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns a group based on its id parameter.
        /// </summary>
        public Group GetGroup(GroupIdParameter groupId)
        {
            var guid = Guid.TryParse(groupId.Value, out Guid id) ? id : Guid.Empty;

            return GetGroup(guid);
        }

        /// <summary>
        /// Retrieves groups matching the query.
        /// </summary>
        public IEnumerable<Group> GetGroups(IQuery<Group> query)
        {
            return ModelHub.GetGroups(query);
        }

        /// <summary>
        /// Returns how many groups satisfy the specified filter criteria without loading
        /// them.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the counted groups. Paging must be left off:
        /// a query carrying it counts the page, not the whole result.
        /// </param>
        /// <returns>The number of matching groups.</returns>
        public int CountGroups(IQuery<Group> query)
        {
            return ModelHub.CountGroups(query);
        }

        /// <summary>
        /// Retrieves groups matching the query with context.
        /// </summary>
        public IEnumerable<Group> GetGroups(IQuery<Group> query, IQueryContext context)
        {
            return ModelHub.GetGroups(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Adds a group.
        /// </summary>
        public IGroupManager Add(Group groupEntity)
        {
            ArgumentNullException.ThrowIfNull(groupEntity);

            ModelHub.Add(groupEntity);

            GroupAdded?.Invoke(this, groupEntity);

            CoreHub.AddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.group.created", groupEntity);

            return this;
        }

        /// <summary>
        /// Updates a group.
        /// </summary>
        public IGroupManager Update(Group groupEntity)
        {
            ArgumentNullException.ThrowIfNull(groupEntity);

            ModelHub.Update(groupEntity);

            GroupUpdated?.Invoke(this, groupEntity);

            CoreHub.AddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.group.updated", groupEntity);

            return this;
        }

        /// <summary>
        /// Removes a group.
        /// </summary>
        /// <remarks>
        /// The built-in groups whose membership is implicit (<see cref="Group.IsImplicit"/>) are
        /// not removed: every workspace a template set up grants its everyday access to one of
        /// them, and the store re-creates them on the next start anyway.
        /// </remarks>
        public IGroupManager Remove(Guid groupId)
        {
            var groupEntry = Group.IsImplicit(groupId) ? null : GetGroup(groupId);

            if (groupEntry is not null)
            {
                ModelHub.Remove(groupEntry);
                GroupRemoved?.Invoke(this, groupEntry);
            }

            return this;
        }

        /// <summary>
        /// Release of unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
