using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebApp.WebRelation;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Administers the relations an <see cref="ObjectRelation"/> may carry and publishes them
    /// into <see cref="RelationRegistry"/>.
    /// </summary>
    /// <remarks>
    /// There is no fixed set of relations. The table is the whole catalog and the registry is
    /// its runtime projection: the registry is rebuilt in memory on every start, so a relation
    /// an administrator defined, changed or deleted only survives a restart because the table
    /// says so. <see cref="Publish"/> therefore replaces the registry's relations outright
    /// rather than adding to them.
    /// </remarks>
    public sealed class ObjectRelationTypeManager : IObjectRelationTypeManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Raised when a relation type has been defined.
        /// </summary>
        public event EventHandler<ObjectRelationType> RelationTypeAdded;

        /// <summary>
        /// Raised when a relation type has been changed.
        /// </summary>
        public event EventHandler<ObjectRelationType> RelationTypeUpdated;

        /// <summary>
        /// Raised when a relation type has been dropped.
        /// </summary>
        public event EventHandler<ObjectRelationType> RelationTypeRemoved;

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private ObjectRelationTypeManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Publishes the stored relations into the framework registry.
        /// </summary>
        public void Publish()
        {
            // the registry is emptied of relations first rather than reset: a reset would put
            // the framework's own eight back, and a relation an administrator deleted would
            // reappear on the next start. The registered link *systems* are deliberately left
            // alone - a system is where a relation may point, not a relation itself
            foreach (var registered in RelationRegistry.Types.ToList())
            {
                RelationRegistry.UnregisterType(registered.Id);
            }

            foreach (var type in ModelHub.GetObjectRelationTypes())
            {
                RelationRegistry.RegisterType(ToRelationType(type));
            }
        }

        /// <summary>
        /// Returns the administered relations in their configured order.
        /// </summary>
        public IEnumerable<ObjectRelationType> GetRelationTypes()
        {
            return ModelHub.GetObjectRelationTypes();
        }

        /// <summary>
        /// Returns a single relation type by its stable key.
        /// </summary>
        public ObjectRelationType GetRelationType(string key)
        {
            return ModelHub.GetObjectRelationType(key);
        }

        /// <summary>
        /// Stores a relation type and republishes the catalog.
        /// </summary>
        public ObjectRelationType Store(ObjectRelationType type)
        {
            ArgumentNullException.ThrowIfNull(type);

            var known = ModelHub.GetObjectRelationType(type.Key) is not null;
            var stored = ModelHub.Store(type);

            Publish();

            if (known)
            {
                RelationTypeUpdated?.Invoke(this, stored);
            }
            else
            {
                RelationTypeAdded?.Invoke(this, stored);
            }

            return stored;
        }

        /// <summary>
        /// Drops the relation type with the supplied key and republishes the catalog.
        /// </summary>
        public bool Remove(string key)
        {
            var stored = ModelHub.GetObjectRelationType(key);

            if (stored is null || !ModelHub.RemoveObjectRelationType(key))
            {
                return false;
            }

            Publish();

            RelationTypeRemoved?.Invoke(this, stored);

            return true;
        }

        /// <summary>
        /// Projects a stored relation onto the shape the framework registry holds.
        /// </summary>
        /// <param name="type">The stored relation type.</param>
        /// <returns>The registry type.</returns>
        private static RelationType ToRelationType(ObjectRelationType type)
        {
            var projected = new RelationType
            {
                Id = type.Key,
                Label = type.Label,

                // a symmetric relation reads alike from both ends, so the counterpart follows
                // the label instead of whatever an earlier edit left in the column
                InverseLabel = type.Symmetric ? type.Label : type.InverseLabel,
                Symmetric = type.Symmetric,
                System = string.IsNullOrWhiteSpace(type.System) ? RelationSystem.Object : type.System,
                Cardinality = type.Cardinality,
                Effect = type.Effect,
                Active = type.Active,
                Icon = string.IsNullOrWhiteSpace(type.Icon) ? "link" : type.Icon,
                Order = type.Order,
                Description = type.Description
            };

            foreach (var name in (type.TargetClasses ?? []).Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                projected.TargetClasses.Add(name);
            }

            return projected;
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
