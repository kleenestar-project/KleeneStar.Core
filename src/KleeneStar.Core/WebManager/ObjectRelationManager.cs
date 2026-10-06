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
    /// Manages the semantic relations an object holds - to other objects of the installation
    /// and to addresses outside it.
    /// </summary>
    public sealed class ObjectRelationManager : IObjectRelationManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Raised when a new relation has been established.
        /// </summary>
        public event EventHandler<ObjectRelation> RelationAdded;

        /// <summary>
        /// Raised when an existing relation has been changed.
        /// </summary>
        public event EventHandler<ObjectRelation> RelationUpdated;

        /// <summary>
        /// Raised when an existing relation has been removed.
        /// </summary>
        public event EventHandler<ObjectRelation> RelationRemoved;

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private ObjectRelationManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns every relation the supplied object takes part in, from either end.
        /// </summary>
        /// <param name="objectId">The id of the object.</param>
        /// <returns>The relations of the object.</returns>
        public IEnumerable<ObjectRelation> GetRelations(Guid objectId)
        {
            var query = new Query<ObjectRelation>()
                .Where(x => x.SourceObjectId == objectId || x.TargetObjectId == objectId);

            return ModelHub.GetObjectRelations(query);
        }

        /// <summary>
        /// Returns every relation running between two of the supplied objects, in one read.
        /// </summary>
        /// <param name="objectIds">The ids of the objects.</param>
        /// <returns>The relations whose source and target are both among the objects.</returns>
        public IEnumerable<ObjectRelation> GetRelationsAmong(IEnumerable<Guid> objectIds)
        {
            var ids = (objectIds ?? []).Distinct().ToList();

            if (ids.Count == 0)
            {
                return [];
            }

            var query = new Query<ObjectRelation>()
                .Where(x => ids.Contains(x.SourceObjectId) && x.TargetObjectId != null && ids.Contains(x.TargetObjectId.Value));

            return ModelHub.GetObjectRelations(query);
        }

        /// <summary>
        /// Returns a single relation by its unique identifier.
        /// </summary>
        /// <param name="id">The id of the relation.</param>
        /// <returns>The relation, or <see langword="null"/> when it is unknown.</returns>
        public ObjectRelation GetRelation(Guid id)
        {
            return ModelHub.GetObjectRelation(id);
        }

        /// <summary>
        /// Returns how many stored relations carry the supplied type.
        /// </summary>
        /// <param name="typeKey">The key of the relation type.</param>
        /// <returns>The number of relations.</returns>
        public int GetUsage(string typeKey)
        {
            return ModelHub.CountObjectRelations(typeKey);
        }

        /// <summary>
        /// Establishes the supplied relation.
        /// </summary>
        public IObjectRelationManager Add(ObjectRelation relation)
        {
            ArgumentNullException.ThrowIfNull(relation);

            ModelHub.Add(relation);

            RelationAdded?.Invoke(this, relation);

            return this;
        }

        /// <summary>
        /// Applies the changeable fields of the supplied relation.
        /// </summary>
        public IObjectRelationManager Update(ObjectRelation relation)
        {
            ArgumentNullException.ThrowIfNull(relation);

            ModelHub.Update(relation);

            RelationUpdated?.Invoke(this, relation);

            return this;
        }

        /// <summary>
        /// Removes the supplied relation.
        /// </summary>
        public IObjectRelationManager Remove(ObjectRelation relation)
        {
            ArgumentNullException.ThrowIfNull(relation);

            ModelHub.Remove(relation);

            RelationRemoved?.Invoke(this, relation);

            return this;
        }

        /// <summary>
        /// Releases unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
