using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Serves the groups a permission dialog can grant a policy to.
    /// </summary>
    /// <remarks>
    /// The groups are the same everywhere — a group is not owned by a resource — so every dialog
    /// takes this list. A resource only needs its own endpoint so its dialog has an address of its
    /// own to ask, which keeps the route next to the rest of that resource's api.
    /// </remarks>
    public abstract class RestApiPermissionGroupsAll : RestApiPermissionGroups
    {
        /// <summary>
        /// Returns the groups that can be granted a policy, by name.
        /// </summary>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The groups, ordered by name.</returns>
        protected override IEnumerable<RestApiPermissionGroup> RetrieveGroups(IRequest request)
        {
            return [.. CoreHub.GroupManager
                .GetGroups(new Query<Model.Entities.Group>())
                .OrderBy(x => x.Name, System.StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new RestApiPermissionGroup()
                {
                    Id = x.Id.ToString(),
                    Name = x.Name
                })];
        }
    }
}
