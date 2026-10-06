using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing the semantic relations an object holds - to other
    /// objects of the installation and to addresses outside it.
    /// </summary>
    public interface IObjectRelationManager : IComponentManager
    {
        /// <summary>
        /// Raised when a new relation has been established.
        /// </summary>
        event EventHandler<ObjectRelation> RelationAdded;

        /// <summary>
        /// Raised when an existing relation has been changed.
        /// </summary>
        event EventHandler<ObjectRelation> RelationUpdated;

        /// <summary>
        /// Raised when an existing relation has been removed.
        /// </summary>
        event EventHandler<ObjectRelation> RelationRemoved;

        /// <summary>
        /// Returns every relation the supplied object takes part in, from either end. One
        /// stored row is one relation, so a relation the object is the target of is answered
        /// here as well and read under the inverse label of its type.
        /// </summary>
        /// <param name="objectId">The id of the object.</param>
        /// <returns>The relations of the object.</returns>
        IEnumerable<ObjectRelation> GetRelations(Guid objectId);

        /// <summary>
        /// Returns every relation running between two of the supplied objects, in one read. A
        /// view showing many objects at once - the Gantt draws the dependencies among its bars -
        /// asks this rather than <see cref="GetRelations(Guid)"/> once per object.
        /// </summary>
        /// <param name="objectIds">The ids of the objects.</param>
        /// <returns>The relations whose source and target are both among the objects.</returns>
        IEnumerable<ObjectRelation> GetRelationsAmong(IEnumerable<Guid> objectIds);

        /// <summary>
        /// Returns a single relation by its unique identifier.
        /// </summary>
        /// <param name="id">The id of the relation.</param>
        /// <returns>The relation, or <see langword="null"/> when it is unknown.</returns>
        ObjectRelation GetRelation(Guid id);

        /// <summary>
        /// Returns how many stored relations carry the supplied type, which the type
        /// administration reports and its delete guards against.
        /// </summary>
        /// <param name="typeKey">The key of the relation type.</param>
        /// <returns>The number of relations.</returns>
        int GetUsage(string typeKey);

        /// <summary>
        /// Establishes the supplied relation.
        /// </summary>
        /// <param name="relation">The relation to add. Cannot be null.</param>
        IObjectRelationManager Add(ObjectRelation relation);

        /// <summary>
        /// Applies the changeable fields of the supplied relation - its type, direction,
        /// lifecycle, note, caption and metadata. The two ends are never moved, because a
        /// relation between other objects is a different relation.
        /// </summary>
        /// <param name="relation">The relation carrying the new values. Cannot be null.</param>
        IObjectRelationManager Update(ObjectRelation relation);

        /// <summary>
        /// Removes the supplied relation.
        /// </summary>
        /// <param name="relation">The relation to remove. Cannot be null.</param>
        IObjectRelationManager Remove(ObjectRelation relation);
    }
}
