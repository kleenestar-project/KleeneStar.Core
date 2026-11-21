using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebApp.WebMessageQueue;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages the additional links shown in the primary area of the app navigator, including adding,
    /// retrieving, and removing, as well as handling link-related events.
    /// </summary>
    public sealed class NavigatorLinkManager : INavigatorLinkManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// An event that fires when a navigator link is added.
        /// </summary>
        public event EventHandler<NavigatorLink> NavigatorLinkAdded;

        /// <summary>
        /// An event that fires when a navigator link is updated.
        /// </summary>
        public event EventHandler<NavigatorLink> NavigatorLinkUpdated;

        /// <summary>
        /// An event that fires when a navigator link is removed.
        /// </summary>
        public event EventHandler<NavigatorLink> NavigatorLinkRemoved;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private NavigatorLinkManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns a navigator link based on its id.
        /// </summary>
        /// <param name="navigatorLinkId">The id of the navigator link.</param>
        /// <returns>The navigator link.</returns>
        public NavigatorLink GetNavigatorLink(Guid navigatorLinkId)
        {
            var query = new Query<NavigatorLink>()
                .Where(x => x.Id == navigatorLinkId)
                .WithPaging(0, 1);

            return ModelHub.GetNavigatorLinks(query)
                .FirstOrDefault();
        }

        /// <summary>
        /// Retrieves a collection of navigator links that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned navigator links. Must not be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of navigator links that match the given predicate. If none
        /// match, the collection will be empty.
        /// </returns>
        public IEnumerable<NavigatorLink> GetNavigatorLinks(IQuery<NavigatorLink> query)
        {
            return ModelHub.GetNavigatorLinks(query);
        }

        /// <summary>
        /// Retrieves a collection of navigator links that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned navigator links. Must not be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of navigator links that match the given predicate. If none
        /// match, the collection will be empty.
        /// </returns>
        public IEnumerable<NavigatorLink> GetNavigatorLinks(IQuery<NavigatorLink> query, IQueryContext context)
        {
            return ModelHub.GetNavigatorLinks(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Returns the active navigator links in the order in which they are shown in the app navigator.
        /// </summary>
        /// <remarks>
        /// The ordering is applied after materialization rather than through the query, so that links
        /// sharing an ordinal keep a stable, name-based order instead of an arbitrary storage order.
        /// </remarks>
        /// <returns>
        /// The active navigator links, ordered by their ordinal and then by name.
        /// </returns>
        public IEnumerable<NavigatorLink> GetVisibleNavigatorLinks()
        {
            var query = new Query<NavigatorLink>()
                .Where(x => x.State == NavigatorLinkState.Active);

            return [.. ModelHub.GetNavigatorLinks(query)
                .OrderBy(x => x.Ordinal)
                .ThenBy(x => x.Name)];
        }

        /// <summary>
        /// Returns all navigator links in the order in which they are listed and shown.
        /// </summary>
        /// <returns>The navigator links, ordered by their ordinal and then by name.</returns>
        public IEnumerable<NavigatorLink> GetOrderedNavigatorLinks()
        {
            return [.. ModelHub.GetNavigatorLinks(new Query<NavigatorLink>())
                .OrderBy(x => x.Ordinal)
                .ThenBy(x => x.Name)];
        }

        /// <summary>
        /// Applies the specified order to the navigator links.
        /// </summary>
        /// <remarks>
        /// The ordinals are reassigned densely from the given sequence rather than shifted, so the
        /// stored order matches what the user arranged and cannot drift apart over repeated moves.
        /// Links the caller did not mention keep their relative order behind the listed ones, which
        /// matters because the table may be showing a filtered page while the order is global.
        /// </remarks>
        /// <param name="orderedIds">The link ids in the desired order.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public INavigatorLinkManager Reorder(IEnumerable<Guid> orderedIds)
        {
            ArgumentNullException.ThrowIfNull(orderedIds);

            var requested = orderedIds.Distinct().ToList();
            var all = GetOrderedNavigatorLinks().ToList();
            var byId = all.ToDictionary(x => x.Id);

            var arranged = requested
                .Where(byId.ContainsKey)
                .Select(x => byId[x])
                .Concat(all.Where(x => !requested.Contains(x.Id)))
                .ToList();

            var changed = false;

            for (var i = 0; i < arranged.Count; i++)
            {
                if (arranged[i].Ordinal == i)
                {
                    continue;
                }

                arranged[i].Ordinal = i;
                ModelHub.Update(arranged[i]);
                NavigatorLinkUpdated?.Invoke(this, arranged[i]);
                changed = true;
            }

            if (changed)
            {
                // the reorder does not travel through the CRUD endpoint, and the table endpoint that
                // receives a dragged order does not announce anything either, so the clients would
                // keep showing the previous order until the page is loaded again
                _ = DataChangedNotifier.NotifyAsync<NavigatorLink>(DataChangeOperation.Updated);
            }

            return this;
        }

        /// <summary>
        /// Moves the specified navigator link one position towards the start or the end.
        /// </summary>
        /// <param name="navigatorLinkId">The id of the link to move.</param>
        /// <param name="up">
        /// <c>true</c> to move the link towards the start; otherwise towards the end.
        /// </param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public INavigatorLinkManager Move(Guid navigatorLinkId, bool up)
        {
            var ordered = GetOrderedNavigatorLinks().ToList();
            var index = ordered.FindIndex(x => x.Id == navigatorLinkId);
            var target = up ? index - 1 : index + 1;

            // an unknown link, or one already at the end it is asked to move towards, is a no-op so
            // a repeated click cannot wrap the entry around to the other end of the list
            if (index < 0 || target < 0 || target >= ordered.Count)
            {
                return this;
            }

            (ordered[index], ordered[target]) = (ordered[target], ordered[index]);

            return Reorder(ordered.Select(x => x.Id));
        }

        /// <summary>
        /// Adds a navigator link to the manager.
        /// </summary>
        /// <param name="navigatorLinkEntity">The navigator link to add. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public INavigatorLinkManager Add(NavigatorLink navigatorLinkEntity)
        {
            ArgumentNullException.ThrowIfNull(navigatorLinkEntity);

            ModelHub.Add(navigatorLinkEntity);

            NavigatorLinkAdded?.Invoke(this, navigatorLinkEntity);

            // create notification
            CoreHub.AddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.navigatorlink.created", navigatorLinkEntity);

            return this;
        }

        /// <summary>
        /// Updates a navigator link of the manager.
        /// </summary>
        /// <param name="navigatorLinkEntity">The navigator link to update. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public INavigatorLinkManager Update(NavigatorLink navigatorLinkEntity)
        {
            ArgumentNullException.ThrowIfNull(navigatorLinkEntity);

            ModelHub.Update(navigatorLinkEntity);

            NavigatorLinkUpdated?.Invoke(this, navigatorLinkEntity);

            // update notification
            CoreHub.AddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.navigatorlink.updated", navigatorLinkEntity);

            return this;
        }

        /// <summary>
        /// Removes the specified navigator link from the manager.
        /// </summary>
        /// <remarks>
        /// If the navigator link does not exist in the manager, no action is taken.
        /// </remarks>
        /// <param name="navigatorLinkId">The navigator link id to be removed.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public INavigatorLinkManager Remove(Guid navigatorLinkId)
        {
            var navigatorLinkEntry = GetNavigatorLink(navigatorLinkId);

            if (navigatorLinkEntry is not null)
            {
                ModelHub.Remove(navigatorLinkEntry);
                NavigatorLinkRemoved?.Invoke(this, navigatorLinkEntry);
            }

            return this;
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
