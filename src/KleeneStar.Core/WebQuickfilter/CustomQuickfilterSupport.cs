using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebIndex;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebQuickfilter
{
    /// <summary>
    /// Adds the quickfilters a user defined to the bar of a view and applies them to its query.
    /// </summary>
    /// <remarks>
    /// Every view wires custom quickfilters in the same two places — the quickfilter endpoint that
    /// fills the bar and the table endpoint that answers with the rows — so both halves live here
    /// rather than being written out per view. Adding a further view is then a matter of calling
    /// <see cref="Items"/> and <see cref="Apply"/> with that view's key.
    /// </remarks>
    public static class CustomQuickfilterSupport
    {
        /// <summary>
        /// Returns the stored quickfilters of a view as items for its bar.
        /// </summary>
        /// <param name="viewKey">The view whose bar is being filled.</param>
        /// <param name="contextKey">
        /// The context that narrows the view, or null for a view that exists only once.
        /// </param>
        /// <param name="request">The request the bar is rendered for.</param>
        /// <returns>
        /// The items to append after the view's own chips. Empty when the user has defined none.
        /// </returns>
        public static IEnumerable<RestApiQuickfilterItem> Items(string viewKey, string contextKey, IRequest request)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            foreach (var filter in CoreHub.CustomQuickfilterManager.GetVisibleCustomQuickfilters(viewKey, contextKey, identityId))
            {
                yield return ToItem(filter);
            }
        }

        /// <summary>
        /// Describes a stored filter as an item of the bar.
        /// </summary>
        /// <remarks>
        /// The item is marked as user-defined and carries its expression, which is what lets the
        /// client offer it for editing and prefill the editor with what was stored.
        /// </remarks>
        /// <param name="filter">The stored filter.</param>
        /// <returns>The item.</returns>
        private static RestApiQuickfilterItem ToItem(CustomQuickfilter filter)
        {
            return new RestApiQuickfilterItem()
            {
                Id = filter.FilterId,
                // the name is what the user typed, so it is offered as written rather than
                // resolved as a translation key
                Name = filter.Name,
                // a shared filter is marked, because the bar otherwise gives no clue why a chip
                // the user never created is being offered
                Icon = filter.Shared ? new IconUsers() : new IconFilter(),
                Custom = true,
                Criteria = filter.Query
            };
        }

        /// <summary>
        /// Stores a filter the user defined in the bar's editor.
        /// </summary>
        /// <param name="payload">The values the client supplied.</param>
        /// <param name="viewKey">The view the filter belongs to.</param>
        /// <param name="contextKey">
        /// The context that narrows the view, or null for a view that exists only once.
        /// </param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The stored filter as an item, or null when it carries no name or expression.</returns>
        public static RestApiQuickfilterItem Create(RestApiQuickfilterPayload payload, string viewKey, string contextKey, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(payload?.Name) || string.IsNullOrWhiteSpace(payload?.Criteria))
            {
                return null;
            }

            var now = DateTime.UtcNow;
            var filter = new CustomQuickfilter(Guid.NewGuid())
            {
                Name = payload.Name.Trim(),
                Query = payload.Criteria,
                ViewKey = viewKey,
                ContextKey = string.IsNullOrWhiteSpace(contextKey) ? null : contextKey,
                OwnerId = CoreHub.SessionManager.GetCurrentIdentityId(request),
                Shared = ReadShared(request),
                Created = now,
                Updated = now
            };

            CoreHub.CustomQuickfilterManager.Add(filter);

            return ToItem(filter);
        }

        /// <summary>
        /// Reads whether the filter is to be offered to everyone.
        /// </summary>
        /// <remarks>
        /// The framework's filter payload has no field for this, and its deserializer drops what it
        /// does not know, so the flag is picked out of the same body here rather than travelling in
        /// an address or a second request.
        /// </remarks>
        /// <param name="request">The request carrying the body.</param>
        /// <returns>True when the body asks for a shared filter.</returns>
        private static bool ReadShared(IRequest request)
        {
            if (request is not Request typed || typed.Content is null || typed.Content.Length == 0)
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(Encoding.UTF8.GetString(typed.Content));

                if (!document.RootElement.TryGetProperty("shared", out var shared))
                {
                    return false;
                }

                return shared.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    // a checkbox reports the html default rather than a boolean
                    JsonValueKind.String => shared.GetString()?.Trim().ToLowerInvariant() is "on" or "true" or "1" or "yes",
                    _ => false
                };
            }
            catch
            {
                // a body that does not parse says nothing about sharing
                return false;
            }
        }

        /// <summary>
        /// Returns the record the edit dialog of a filter loads.
        /// </summary>
        /// <remarks>
        /// The framework's own record carries what every filter has; this one adds whether the
        /// filter is shared, so the dialog shows the switch as it stands and sending the record
        /// back unchanged does not quietly un-share it.
        /// </remarks>
        /// <param name="filterId">The chip id the client reported.</param>
        /// <param name="viewKey">The view the filter must belong to.</param>
        /// <returns>The record, or null when the id denotes none of this view's.</returns>
        public static object Read(string filterId, string viewKey)
        {
            var filter = Resolve(filterId, viewKey);

            if (filter is null)
            {
                return null;
            }

            return new
            {
                id = filter.FilterId,
                name = filter.Name,
                criteria = filter.Query,
                shared = filter.Shared
            };
        }

        /// <summary>
        /// Changes a filter the user defined.
        /// </summary>
        /// <remarks>
        /// The view a filter belongs to and its owner are not taken from the payload, so an edit
        /// cannot move a filter into another bar or hand it to somebody else. Sharing is read from
        /// the body the same way it is on creation, so the switch in the dialog takes effect in
        /// both directions.
        /// </remarks>
        /// <param name="payload">The values the client supplied.</param>
        /// <param name="viewKey">The view the filter must belong to.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The changed filter as an item, or null when there is no such filter here.</returns>
        public static RestApiQuickfilterItem Update(RestApiQuickfilterPayload payload, string viewKey, IRequest request)
        {
            var filter = Resolve(payload?.Id, viewKey);

            if (filter is null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(payload.Name))
            {
                filter.Name = payload.Name.Trim();
            }

            if (!string.IsNullOrWhiteSpace(payload.Criteria))
            {
                filter.Query = payload.Criteria;
            }

            filter.Shared = ReadShared(request);

            CoreHub.CustomQuickfilterManager.Update(filter);

            return ToItem(filter);
        }

        /// <summary>
        /// Removes a filter the user defined.
        /// </summary>
        /// <param name="filterId">The chip id the client reported.</param>
        /// <param name="viewKey">The view the filter must belong to.</param>
        /// <returns>True when a filter was removed.</returns>
        public static bool Delete(string filterId, string viewKey)
        {
            var filter = Resolve(filterId, viewKey);

            if (filter is null)
            {
                return false;
            }

            CoreHub.CustomQuickfilterManager.Remove(filter.Id);

            return true;
        }

        /// <summary>
        /// Returns the stored filter a chip id denotes, provided it belongs to the view.
        /// </summary>
        /// <param name="filterId">The chip id the client reported.</param>
        /// <param name="viewKey">The view the filter must belong to.</param>
        /// <returns>The filter, or null when the id denotes none of this view's.</returns>
        private static CustomQuickfilter Resolve(string filterId, string viewKey)
        {
            var id = CustomQuickfilter.ParseFilterId(filterId);

            if (id is null)
            {
                return null;
            }

            var filter = CoreHub.CustomQuickfilterManager.GetCustomQuickfilter(id.Value);

            return filter is not null && string.Equals(filter.ViewKey, viewKey, StringComparison.OrdinalIgnoreCase)
                ? filter
                : null;
        }

        /// <summary>
        /// Applies the stored quickfilters among the active chips to a query.
        /// </summary>
        /// <remarks>
        /// The filters are composed onto the running query rather than replacing it, so a stored
        /// filter combines with the view's own chips and with the search term instead of discarding
        /// them. A filter whose expression no longer parses is skipped: the view stays usable and
        /// the remaining chips keep working, which matters because the expression was typed by hand
        /// and the fields it names can be removed later.
        /// </remarks>
        /// <typeparam name="TIndexItem">The type the view lists.</typeparam>
        /// <param name="filters">The quickfilter ids reported as active.</param>
        /// <param name="query">The query to narrow.</param>
        /// <param name="viewKey">The view the filters must belong to.</param>
        /// <returns>The narrowed query.</returns>
        public static IQuery<TIndexItem> Apply<TIndexItem>(IEnumerable<string> filters, IQuery<TIndexItem> query, string viewKey)
            where TIndexItem : IIndexItem
        {
            foreach (var predicate in Predicates<TIndexItem>(filters, viewKey))
            {
                query = query.Where(predicate);
            }

            return query;
        }

        /// <summary>
        /// Applies the stored quickfilters among the active chips to a materialized sequence.
        /// </summary>
        /// <remarks>
        /// Views that answer from memory rather than from a query — because their rows are composed
        /// from several sources — narrow the sequence instead. The stored expression is the same
        /// one either way, only compiled here rather than handed to the query.
        /// </remarks>
        /// <typeparam name="TIndexItem">The type the view lists.</typeparam>
        /// <param name="filters">The quickfilter ids reported as active.</param>
        /// <param name="items">The sequence to narrow.</param>
        /// <param name="viewKey">The view the filters must belong to.</param>
        /// <returns>The narrowed sequence.</returns>
        public static IEnumerable<TIndexItem> Apply<TIndexItem>(IEnumerable<string> filters, IEnumerable<TIndexItem> items, string viewKey)
            where TIndexItem : IIndexItem
        {
            foreach (var predicate in Predicates<TIndexItem>(filters, viewKey))
            {
                items = items.Where(predicate.Compile());
            }

            return items;
        }

        /// <summary>
        /// Returns the conditions of the stored quickfilters among the active chips.
        /// </summary>
        /// <remarks>
        /// A filter of another view is skipped even when its id is passed in, because its
        /// expression names fields this type does not have.
        /// </remarks>
        /// <typeparam name="TIndexItem">The type the view lists.</typeparam>
        /// <param name="filters">The quickfilter ids reported as active.</param>
        /// <param name="viewKey">The view the filters must belong to.</param>
        /// <returns>One condition per applicable filter.</returns>
        private static IEnumerable<Expression<Func<TIndexItem, bool>>> Predicates<TIndexItem>(IEnumerable<string> filters, string viewKey)
            where TIndexItem : IIndexItem
        {
            foreach (var filterId in filters ?? [])
            {
                var id = CustomQuickfilter.ParseFilterId(filterId);

                if (id is null)
                {
                    continue;
                }

                var filter = CoreHub.CustomQuickfilterManager.GetCustomQuickfilter(id.Value);

                if (filter is null || !string.Equals(filter.ViewKey, viewKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var predicate = Compile<TIndexItem>(filter.Query);

                if (predicate is not null)
                {
                    yield return predicate;
                }
            }
        }

        /// <summary>
        /// Turns a WQL expression into a condition.
        /// </summary>
        /// <remarks>
        /// The board filters compile the same way (<see cref="WqlFilter"/>), so a stored
        /// expression means the same thing wherever it is applied.
        /// </remarks>
        /// <typeparam name="TIndexItem">The type the view lists.</typeparam>
        /// <param name="wql">The expression to compile.</param>
        /// <returns>
        /// The condition, or null when the expression is empty, carries none, or no longer parses.
        /// </returns>
        private static Expression<Func<TIndexItem, bool>> Compile<TIndexItem>(string wql)
            where TIndexItem : IIndexItem
        {
            return WqlFilter.Compile<TIndexItem>(wql);
        }
    }
}
