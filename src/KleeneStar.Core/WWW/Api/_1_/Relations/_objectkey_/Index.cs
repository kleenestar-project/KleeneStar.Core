using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRelation;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WWW.Api._1_.Relations._objectkey_
{
    /// <summary>
    /// REST endpoint backing the <c>ControlDataRelationView</c> on an object's detail and
    /// preview pages. The URL is <c>/api/1/relations/{objectkey}</c>; the <c>{objectkey}</c>
    /// segment is declared via <see cref="ObjectKeySegmentAttribute"/> so the control's data
    /// island binds it from the current request.
    /// </summary>
    /// <remarks>
    /// <see cref="RestApiRelation"/> implements the whole generic half - the filtering, the
    /// grouping by relation, the perspective that decides which of a relation's two labels
    /// applies, and the validation against the registry the relation catalog is published
    /// into. What is left here are the storage questions, which is why this class only
    /// resolves objects and delegates to <see cref="CoreHub.ObjectRelationManager"/>.
    /// <para>
    /// <see cref="IncludeSubPathsAttribute"/> is REQUIRED: the change and the removal of a
    /// single relation address it as <c>/api/1/relations/{objectkey}/{id}</c>, and without it
    /// those sub-paths 404 and the surface silently loses both actions.
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:relation.api.title")]
    [ObjectKeySegment]
    [IncludeSubPaths(true)]
    [Cache]
    public sealed class Index : RestApiRelation
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Answers the relations of the addressed object, once the caller may read it.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.GET)]
        public override IResponse Retrieve(IRequest request)
        {
            return Authorized(request, write: false) ? base.Retrieve(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Establishes a relation, once the caller may change the object's relations.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.POST)]
        public override IResponse Create(IRequest request)
        {
            return Authorized(request, write: true) ? base.Create(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Changes a relation, once the caller may change the object's relations.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.PUT)]
        public override IResponse Update(IRequest request)
        {
            return Authorized(request, write: true) ? base.Update(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Removes a relation, once the caller may change the object's relations.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.DELETE)]
        public override IResponse Delete(IRequest request)
        {
            return Authorized(request, write: true) ? base.Delete(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Determines whether the caller may perform the requested kind of access on the object
        /// the route addresses.
        /// </summary>
        /// <remarks>
        /// The check is issued against the object, its class and its workspace, so a grant on any
        /// of them governs the relations of the object. An installation that has administered
        /// none of the three is unrestricted; see
        /// <see cref="WebManager.IPermissionManager.IsGranted"/>.
        /// </remarks>
        /// <param name="request">The incoming request.</param>
        /// <param name="write">Whether the request changes something.</param>
        /// <returns><see langword="true"/> when the request may proceed.</returns>
        private static bool Authorized(IRequest request, bool write)
        {
            var @object = ObjectRelationProjection.ResolveSubject(request);

            return write
                ? ObjectRelationAuthorization.MayWrite(@object, request)
                : ObjectRelationAuthorization.MayRead(@object, request);
        }

        /// <summary>
        /// Returns the object the route addresses - the object whose relations are answered,
        /// the source of every relation established here, and the end all of them are read
        /// from.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The reference, or <see langword="null"/> when the route names no object.</returns>
        protected override RelationReference RetrieveSubject(IRequest request)
        {
            return ObjectRelationProjection.ToReference(ObjectRelationProjection.ResolveSubject(request));
        }

        /// <summary>
        /// Returns the relations of the addressed object, narrowed by the filter.
        /// </summary>
        /// <remarks>
        /// The store answers every relation an object takes part in and the criteria are
        /// applied in memory afterwards, which is what
        /// <see cref="RestApiRelationFilter.Matches"/> exists for. A relation surface holds
        /// the relations of one object rather than of a workspace, so the set being narrowed
        /// is small and an indexed query per criterion would buy nothing.
        /// </remarks>
        /// <param name="filter">The criteria, with the category already removed.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The matching relations.</returns>
        protected override IEnumerable<Relation> RetrieveLinks(RestApiRelationFilter filter, IRequest request)
        {
            var subject = ObjectRelationProjection.ResolveObject(filter?.Source)
                ?? ObjectRelationProjection.ResolveSubject(request);

            if (subject is null)
            {
                return [];
            }

            return CoreHub.ObjectRelationManager
                .GetRelations(subject.Id)
                .Select(ObjectRelationProjection.ToRelation)
                .Where(x => x is not null)
                .Where(x => filter is null || filter.Matches(x, KindOf(x)))
                .ToList();
        }

        /// <summary>
        /// Returns a single stored relation.
        /// </summary>
        /// <param name="id">The identity of the relation.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The relation, or <see langword="null"/> when it is unknown.</returns>
        protected override Relation RetrieveLink(string id, IRequest request)
        {
            return Guid.TryParse(id, out var parsed)
                ? ObjectRelationProjection.ToRelation(CoreHub.ObjectRelationManager.GetRelation(parsed))
                : null;
        }

        /// <summary>
        /// Persists a validated relation and answers it with its assigned identity.
        /// </summary>
        /// <param name="relation">The validated relation.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The stored relation, or <see langword="null"/> when it was rejected.</returns>
        protected override Relation CreateLink(Relation relation, IRequest request)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var entity = ObjectRelationProjection.ToEntity(relation, identityId);

            if (entity is null)
            {
                return null;
            }

            CoreHub.ObjectRelationManager.Add(entity);

            return ObjectRelationProjection.ToRelation(CoreHub.ObjectRelationManager.GetRelation(entity.Id));
        }

        /// <summary>
        /// Persists the changed fields of a validated relation. The two ends stay where they
        /// are - the base class already refuses to move them, and the store does not offer it.
        /// </summary>
        /// <param name="relation">The validated relation.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The stored relation, or <see langword="null"/> when it was rejected.</returns>
        protected override Relation UpdateLink(Relation relation, IRequest request)
        {
            if (!Guid.TryParse(relation?.Id, out var id))
            {
                return null;
            }

            var stored = CoreHub.ObjectRelationManager.GetRelation(id);

            if (stored is null)
            {
                return null;
            }

            stored.TypeKey = relation.Type;
            stored.Direction = relation.Direction;
            stored.Status = relation.Status;
            stored.Comment = relation.Comment;

            // only an external end carries a caption of its own; an object end is named by the
            // object, so a title arriving for one is dropped rather than stored beside it
            if (stored.TargetObjectId is null)
            {
                stored.TargetTitle = relation.Target?.Title;
            }

            stored.Metadata = new Dictionary<string, string>(relation.Metadata ?? new Dictionary<string, string>());

            CoreHub.ObjectRelationManager.Update(stored);

            return ObjectRelationProjection.ToRelation(CoreHub.ObjectRelationManager.GetRelation(id));
        }

        /// <summary>
        /// Removes a relation.
        /// </summary>
        /// <param name="id">The identity of the relation.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the relation existed and was removed.</returns>
        protected override bool DeleteLink(string id, IRequest request)
        {
            if (!Guid.TryParse(id, out var parsed))
            {
                return false;
            }

            var stored = CoreHub.ObjectRelationManager.GetRelation(parsed);

            if (stored is null)
            {
                return false;
            }

            CoreHub.ObjectRelationManager.Remove(stored);

            return true;
        }

        /// <summary>
        /// Determines whether a referenced object exists, so a relation can never be stored
        /// against a key that was mistyped or an object that was meanwhile deleted.
        /// </summary>
        /// <param name="reference">The reference to resolve.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the object exists.</returns>
        protected override bool Exists(RelationReference reference, IRequest request)
        {
            // an external end is addressed by its uri and has no object to resolve; the
            // framework has already checked that the address is absolute
            return reference is null
                || !reference.IsObject()
                || ObjectRelationProjection.ResolveObject(reference.Key) is not null;
        }

        /// <summary>
        /// Returns the relations that already touch either end of a candidate, which the
        /// duplicate and the cardinality checks are evaluated against.
        /// </summary>
        /// <param name="relation">The candidate.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The neighbouring relations.</returns>
        protected override IEnumerable<Relation> RetrieveNeighbourhood(Relation relation, IRequest request)
        {
            var ends = new[] { relation?.Source?.Key, relation?.Target?.Key }
                .Select(ObjectRelationProjection.ResolveObject)
                .Where(x => x is not null)
                .Select(x => x.Id)
                .Distinct()
                .ToList();

            if (ends.Count == 0)
            {
                return [];
            }

            // both ends are asked, so a relation stored from the other side still counts
            // against the cardinality of this one
            return ends
                .SelectMany(CoreHub.ObjectRelationManager.GetRelations)
                .DistinctBy(x => x.Id)
                .Select(ObjectRelationProjection.ToRelation)
                .Where(x => x is not null)
                .ToList();
        }
    }
}
