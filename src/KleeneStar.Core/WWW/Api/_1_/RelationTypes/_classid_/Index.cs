using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRelation;
using WebExpress.WebApp.WebRestApi;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using ClassEntity = KleeneStar.Model.Entities.Class;

namespace KleeneStar.Core.WWW.Api._1_.RelationTypes._classid_
{
    /// <summary>
    /// REST endpoint backing the <c>ControlDataRelationEditor</c> in the class administration:
    /// the relations objects of a class may hold, and the editor that defines and changes
    /// them. The URL is <c>/api/1/relationtypes/{classid}</c>.
    /// </summary>
    /// <remarks>
    /// There is no fixed set of relations. Every row the editor writes is an
    /// <see cref="ObjectRelationType"/> an administrator invented, and the table is the whole
    /// catalog - which is why a definition goes through
    /// <see cref="WebManager.IObjectRelationTypeManager.Store"/> rather than being registered
    /// directly: the manager persists it and republishes the catalog, so the change reaches
    /// every link surface and the add dialog in the same request.
    /// <para>
    /// The relations themselves are installation-wide rather than owned by a class - a
    /// relation that only ever joined one class would say very little. The <c>{classid}</c>
    /// segment names the class the surface is administered from: it narrows the table to the
    /// relations that accept the class and is the class the editor writes its preview from.
    /// </para>
    /// <para>
    /// <see cref="IncludeSubPathsAttribute"/> is REQUIRED: the change, the removal and the
    /// reordering address <c>/api/1/relationtypes/{classid}/{id}</c> and
    /// <c>/api/1/relationtypes/{classid}/order</c>, and without it those sub-paths 404 and the
    /// editor silently loses all three.
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:relation.type.api.title")]
    [ClassIdSegment]
    [IncludeSubPaths(true)]
    [Cache]
    public sealed class Index : RestApiRelationType
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Answers the catalog, once the caller may read the class it is administered from.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.GET)]
        public override IResponse Retrieve(IRequest request)
        {
            return ObjectRelationAuthorization.MayReadCatalog(ObjectRelationAuthorization.ResolveClassId(request), request)
                ? base.Retrieve(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Defines a relation or rearranges the catalog, once the caller may change the class.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.POST)]
        public override IResponse Create(IRequest request)
        {
            return AuthorizedToWrite(request) ? base.Create(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Changes a relation, once the caller may change the class.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.PUT)]
        public override IResponse Update(IRequest request)
        {
            return AuthorizedToWrite(request) ? base.Update(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Drops a relation, once the caller may change the class.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.DELETE)]
        public override IResponse Delete(IRequest request)
        {
            return AuthorizedToWrite(request) ? base.Delete(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Determines whether the caller may change the catalog administered from the class the
        /// route addresses.
        /// </summary>
        /// <remarks>
        /// Defining a relation is a change to the class the surface is administered from, so it
        /// is governed by the class permission model rather than by the object one: the relations
        /// a class may hold outlive every object that uses them.
        /// </remarks>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the change may proceed.</returns>
        private static bool AuthorizedToWrite(IRequest request)
        {
            return ObjectRelationAuthorization.MayWriteCatalog(ObjectRelationAuthorization.ResolveClassId(request), request);
        }

        /// <summary>
        /// Returns the administered relations in their configured order.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The relation types.</returns>
        protected override IEnumerable<IRelationType> RetrieveTypes(IRequest request)
        {
            // the registry is answered rather than the table, because it is the projection the
            // rest of the contract is evaluated against - the labels it carries are the ones
            // the surfaces group by, and reading it here keeps the two from disagreeing
            return RelationRegistry.Types;
        }

        /// <summary>
        /// Persists a defined or edited relation and republishes the catalog.
        /// </summary>
        /// <param name="type">The relation to store.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The stored relation, or <see langword="null"/> when it was rejected.</returns>
        protected override IRelationType StoreType(RelationType type, IRequest request)
        {
            if (type is null || string.IsNullOrWhiteSpace(type.Id))
            {
                return null;
            }

            var stored = CoreHub.ObjectRelationTypeManager.GetRelationType(type.Id) ?? new ObjectRelationType
            {
                Key = type.Id,
                Created = DateTime.UtcNow
            };

            stored.Label = type.Label;
            stored.InverseLabel = type.Symmetric ? type.Label : type.InverseLabel;
            stored.Symmetric = type.Symmetric;
            stored.System = string.IsNullOrWhiteSpace(type.System) ? RelationSystem.Object : type.System;
            stored.TargetClasses = [.. type.TargetClasses.Where(x => !string.IsNullOrWhiteSpace(x))];
            stored.Cardinality = type.Cardinality;
            stored.Effect = type.Effect;
            stored.Active = type.Active;
            stored.Icon = type.Icon;
            stored.Order = type.Order;
            stored.Description = type.Description;

            CoreHub.ObjectRelationTypeManager.Store(stored);

            return RelationRegistry.GetType(type.Id);
        }

        /// <summary>
        /// Drops a relation that carries no stored relations. The base class has already refused
        /// the request when it is still in use.
        /// </summary>
        /// <param name="id">The key of the relation.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the relation existed and was removed.</returns>
        protected override bool RemoveType(string id, IRequest request)
        {
            return CoreHub.ObjectRelationTypeManager.Remove(id);
        }

        /// <summary>
        /// Returns how many stored relations carry the type, which the table shows and the
        /// delete guards against.
        /// </summary>
        /// <param name="id">The key of the relation.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The number of relations.</returns>
        protected override int RetrieveUsage(string id, IRequest request)
        {
            return CoreHub.ObjectRelationManager.GetUsage(id);
        }

        /// <summary>
        /// Returns the classes the editor offers when a relation is narrowed to certain
        /// targets.
        /// </summary>
        /// <remarks>
        /// The classes of the workspace the administered class belongs to are offered rather
        /// than every class of the installation: a relation is only ever established between
        /// two objects a person can see at once, and a checkbox list of every class of every
        /// workspace would be unusable long before it was useful. The class is identified by
        /// its name, because that is what the reference of a stored relation carries and what
        /// the framework validates a target against.
        /// </remarks>
        /// <param name="request">The incoming request.</param>
        /// <returns>The classes.</returns>
        protected override IEnumerable<RestApiRelationClassItem> RetrieveClasses(IRequest request)
        {
            var administered = ResolveClass(request);

            if (administered is null)
            {
                return [];
            }

            var query = new Query<ClassEntity>()
                .Where(x => x.WorkspaceId == administered.WorkspaceId);

            return CoreHub.ClassManager
                .GetClasses(query)
                .Where(x => x.State == ClassState.Active)
                .Select(x => x.Name)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Select(x => new RestApiRelationClassItem { Id = x, Label = x })
                .ToList();
        }

        /// <summary>
        /// Rearranges the relations. The whole resulting order travels in one request, so the
        /// stored positions are rewritten in one pass rather than shifted relative to a single
        /// moved row.
        /// </summary>
        /// <param name="ids">The keys in their new order.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the order was applied.</returns>
        protected override bool ApplyOrder(IReadOnlyList<string> ids, IRequest request)
        {
            var applied = false;

            for (var i = 0; i < ids.Count; i++)
            {
                var stored = CoreHub.ObjectRelationTypeManager.GetRelationType(ids[i]);

                if (stored is null)
                {
                    continue;
                }

                stored.Order = i + 1;

                CoreHub.ObjectRelationTypeManager.Store(stored);
                applied = true;
            }

            return applied;
        }

        /// <summary>
        /// Resolves the class the surface is administered from.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The class, or <see langword="null"/> when the route names none.</returns>
        private static ClassEntity ResolveClass(IRequest request)
        {
            var parameter = request?.GetParameter<ClassIdParameter>();

            return Guid.TryParse(parameter?.Value, out var id)
                ? CoreHub.ClassManager.GetClass(id)
                : null;
        }
    }
}
