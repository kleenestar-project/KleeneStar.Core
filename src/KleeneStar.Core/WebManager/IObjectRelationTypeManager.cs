using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebApp.WebRelation;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for administering the relations an <see cref="ObjectRelation"/>
    /// may carry, and for publishing them into the framework registry every link surface
    /// reads.
    /// </summary>
    public interface IObjectRelationTypeManager : IComponentManager
    {
        /// <summary>
        /// Raised when a relation type has been defined or changed.
        /// </summary>
        event EventHandler<ObjectRelationType> RelationTypeAdded;

        /// <summary>
        /// Raised when a relation type has been changed.
        /// </summary>
        event EventHandler<ObjectRelationType> RelationTypeUpdated;

        /// <summary>
        /// Raised when a relation type has been dropped.
        /// </summary>
        event EventHandler<ObjectRelationType> RelationTypeRemoved;

        /// <summary>
        /// Publishes the stored relations into <see cref="RelationRegistry"/>, so the relation
        /// surface, the add dialog and the validation all read the administered catalog rather
        /// than the framework defaults. Called once at startup and after every write.
        /// </summary>
        void Publish();

        /// <summary>
        /// Returns the administered relations in their configured order.
        /// </summary>
        /// <returns>The relation types.</returns>
        IEnumerable<ObjectRelationType> GetRelationTypes();

        /// <summary>
        /// Returns a single relation type by its stable key.
        /// </summary>
        /// <param name="key">The key of the relation type.</param>
        /// <returns>The relation type, or <see langword="null"/> when it is unknown.</returns>
        ObjectRelationType GetRelationType(string key);

        /// <summary>
        /// Stores a relation type and republishes the catalog. A key that is not yet taken
        /// defines a new relation; a known key overwrites the stored definition.
        /// </summary>
        /// <param name="type">The relation type to store. Cannot be null.</param>
        /// <returns>The stored relation type.</returns>
        ObjectRelationType Store(ObjectRelationType type);

        /// <summary>
        /// Drops the relation type with the supplied key and republishes the catalog.
        /// </summary>
        /// <param name="key">The key of the relation type.</param>
        /// <returns><see langword="true"/> when the type existed and was removed.</returns>
        bool Remove(string key);
    }
}
