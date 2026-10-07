using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

// The entity type Object collides with System.Object; alias it so the signatures read
// naturally.
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Narrows a kind overview to one class: the overview page carries the class as
    /// <c>?class=&lt;id&gt;</c> (the class tree of the asset sidebar links there), the views on
    /// it hand the same query on to their endpoints, and the endpoints keep the objects of
    /// that class and of every class inheriting from it.
    /// </summary>
    /// <remarks>
    /// Inheritance is followed downwards because an object of a derived class <i>is</i> one
    /// of its base class - a server is hardware - and because an abstract class has no
    /// objects of its own, so a click on it would otherwise always answer with nothing. The
    /// lineage is computed over every class, not only those of the workspace, since a class
    /// may inherit from one of a base workspace; the workspace scope of the endpoint still
    /// decides which objects are listed. An address naming a class that does not exist
    /// narrows to nothing rather than to everything: an empty list says the class is gone,
    /// a full one would pass for its content.
    /// </remarks>
    public static class ObjectClassFilter
    {
        /// <summary>
        /// The query key the class travels under.
        /// </summary>
        public const string Parameter = "class";

        /// <summary>
        /// Determines whether the request names a class, resolvable or not.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the request carries the class parameter.</returns>
        public static bool IsRequested(IRequest request)
        {
            return !string.IsNullOrWhiteSpace(request?.GetParameter(Parameter)?.Value);
        }

        /// <summary>
        /// Returns the class the request names.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The class, or <see langword="null"/> when none is named or it does not exist.</returns>
        public static Class Resolve(IRequest request)
        {
            var value = request?.GetParameter(Parameter)?.Value;

            return Guid.TryParse(value, out var id) && id != Guid.Empty
                ? CoreHub.ClassManager.GetClass(id)
                : null;
        }

        /// <summary>
        /// Returns the ids of the classes the request narrows to: the named class and every
        /// class inheriting from it, directly or not.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>
        /// The class ids, <see langword="null"/> when the request names no class, and an empty
        /// set when it names one that does not exist.
        /// </returns>
        public static IReadOnlySet<Guid> ResolveLineage(IRequest request)
        {
            if (!IsRequested(request))
            {
                return null;
            }

            var root = Resolve(request);

            return root is null
                ? new HashSet<Guid>()
                : Lineage(root.Id, CoreHub.ClassManager.GetClasses(new Query<Class>()));
        }

        /// <summary>
        /// Collects a class and every class inheriting from it, directly or not.
        /// </summary>
        /// <param name="rootId">The class the lineage starts at.</param>
        /// <param name="classes">The classes to search for descendants.</param>
        /// <returns>The ids of the class and its descendants.</returns>
        public static IReadOnlySet<Guid> Lineage(Guid rootId, IEnumerable<Class> classes)
        {
            var derived = (classes ?? [])
                .Where(x => x.InheritedId.HasValue && x.InheritedId.Value != x.Id)
                .GroupBy(x => x.InheritedId.Value)
                .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToList());

            // the visited set doubles as the result and breaks an inheritance cycle that
            // older data may still carry
            var lineage = new HashSet<Guid> { rootId };
            var pending = new Queue<Guid>(lineage);

            while (pending.Count > 0)
            {
                if (!derived.TryGetValue(pending.Dequeue(), out var children))
                {
                    continue;
                }

                foreach (var child in children.Where(lineage.Add))
                {
                    pending.Enqueue(child);
                }
            }

            return lineage;
        }

        /// <summary>
        /// Narrows an object query to the class the request names, and leaves it alone when
        /// the request names none.
        /// </summary>
        /// <param name="query">The query to narrow.</param>
        /// <param name="request">The request.</param>
        /// <returns>The narrowed query.</returns>
        public static IQuery<ObjectEntity> Apply(IQuery<ObjectEntity> query, IRequest request)
        {
            var lineage = ResolveLineage(request);

            if (lineage is null)
            {
                return query;
            }

            var ids = lineage.ToList();

            return query.Where(x => ids.Contains(x.ClassId));
        }

        /// <summary>
        /// Hands the class of the page request on to a data service address, so the endpoint
        /// behind a view of the page narrows the way the page does.
        /// </summary>
        /// <param name="baseUri">The address of the endpoint.</param>
        /// <param name="request">The page request.</param>
        /// <returns>The address, carrying the class when the page names one.</returns>
        public static string Carry(string baseUri, IRequest request)
        {
            var value = request?.GetParameter(Parameter)?.Value;

            if (string.IsNullOrWhiteSpace(baseUri) || !Guid.TryParse(value, out var id))
            {
                return baseUri;
            }

            var separator = baseUri.Contains('?') ? "&" : "?";

            return $"{baseUri}{separator}{Parameter}={id}";
        }
    }
}
