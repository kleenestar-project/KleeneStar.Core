using KleeneStar.Core.WebRestApi;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRelation;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WWW.Api._1_.Relations
{
    /// <summary>
    /// REST endpoint the target combo box of the add dialog searches through: the objects a
    /// relation may point at. The URL is <c>/api/1/relations/targets</c>.
    /// </summary>
    /// <remarks>
    /// The candidates are narrowed by the relation the user picked, because which classes a
    /// relation accepts is part of its definition - offering an object the endpoint would then
    /// refuse would make the dialog lie. With no term typed the answer is what the calling
    /// identity opened last, which is far more often the object they mean than the first row
    /// of an alphabetical list.
    /// </remarks>
    [Title("kleenestar.core:relation.targets.api.title")]
    [Cache]
    public sealed class Targets : RestApiRelationTarget
    {
        /// <summary>
        /// The number of recently opened objects offered before anything is typed.
        /// </summary>
        private const int MaxRecent = 10;

        /// <summary>
        /// The number of matches read from the store before the accepted classes narrow them,
        /// which has to exceed the page size the dialog shows so a filtered relation still
        /// fills its list.
        /// </summary>
        private const int MaxCandidates = 100;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Targets()
        {
        }

        /// <summary>
        /// Returns the candidates for the target of a relation.
        /// </summary>
        /// <param name="search">The search term, possibly empty.</param>
        /// <param name="type">The key of the relation the relation will carry, may be absent.</param>
        /// <param name="system">The id of the link system, may be absent.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The candidates.</returns>
        protected override IEnumerable<RestApiRelationReference> RetrieveTargets(string search, string type, string system, IRequest request)
        {
            // an external system addresses its target by uri, so there is nothing here to
            // offer for it
            if (RelationRegistry.GetSystem(system)?.Kind == RelationKind.External)
            {
                return [];
            }

            var accepted = RelationRegistry.GetType(type)?.TargetClasses?.ToList() ?? [];

            return Candidates(search, request)
                .Where(x => Accepts(accepted, x))
                .Select(ObjectRelationProjection.ToWireReference)
                .ToList();
        }

        /// <summary>
        /// Returns the objects a search term matches, or the recently opened ones when nothing
        /// was typed.
        /// </summary>
        /// <param name="search">The search term, possibly empty.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The candidate objects.</returns>
        private static IEnumerable<ObjectEntity> Candidates(string search, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

                return CoreHub.ObjectManager.GetRecentObjects(ownerId, MaxRecent);
            }

            // the key is what a person pastes from a ticket mail and the summary is what they
            // remember, so both are searched rather than only the one the list shows
            var query = new Query<ObjectEntity>()
                .WhereContainsIgnoreCase(x => x.Summary, search)
                .Or(new Query<ObjectEntity>().WhereContainsIgnoreCase(x => x.Key, search))
                .WithPaging(0, MaxCandidates);

            return CoreHub.ObjectManager.GetObjects(query);
        }

        /// <summary>
        /// Determines whether a relation accepts the class of a candidate. A relation that
        /// names no classes accepts every one of them.
        /// </summary>
        /// <param name="accepted">The class names the relation accepts.</param>
        /// <param name="object">The candidate.</param>
        /// <returns><see langword="true"/> when the candidate may be picked.</returns>
        private static bool Accepts(IReadOnlyCollection<string> accepted, ObjectEntity @object)
        {
            return accepted.Count == 0
                || accepted.Contains(ObjectRelationProjection.ClassNameOf(@object), StringComparer.OrdinalIgnoreCase);
        }
    }
}
