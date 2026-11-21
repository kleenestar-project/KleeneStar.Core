using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Provides the structure of blueprint classes to the classes that follow them.
    /// </summary>
    /// <remarks>
    /// A blueprint (<see cref="Class.IsBlueprint"/>) defines fields, statuses, priorities and
    /// workflows once; a follower (<see cref="Class.BlueprintId"/>) in any workspace receives them
    /// as managed copies and is kept in step whenever the blueprint changes. Views, boards,
    /// forms, filters, templates and local additions stay the follower's own, so every workspace
    /// keeps its way of working on a shared data structure.
    /// </remarks>
    public interface IBlueprintManager : IComponentManager
    {
        /// <summary>
        /// Returns the classes following a blueprint.
        /// </summary>
        /// <param name="blueprintId">The blueprint class.</param>
        /// <returns>The followers.</returns>
        IReadOnlyList<Class> GetFollowers(Guid blueprintId);

        /// <summary>
        /// Brings a follower in step with its blueprint.
        /// </summary>
        /// <param name="followerId">The following class.</param>
        /// <returns>What the synchronization changed.</returns>
        BlueprintSyncResult Synchronize(Guid followerId);

        /// <summary>
        /// Brings every follower of a blueprint in step with it.
        /// </summary>
        /// <param name="blueprintId">The blueprint class.</param>
        void SynchronizeFollowers(Guid blueprintId);

        /// <summary>
        /// Creates, in a workspace, a follower of every blueprint class of another workspace -
        /// the way a workspace is set up from a blueprint workspace.
        /// </summary>
        /// <param name="blueprintWorkspaceId">The workspace holding the blueprint classes.</param>
        /// <param name="workspaceId">The workspace the followers are created in.</param>
        /// <returns>The created followers.</returns>
        IReadOnlyList<Class> ApplyWorkspace(Guid blueprintWorkspaceId, Guid workspaceId);

        /// <summary>
        /// Determines whether an element of a class structure is a managed copy of a blueprint
        /// element, which is changed in the blueprint rather than locally.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns><see langword="true"/> for a managed copy.</returns>
        bool IsManaged(IBlueprintManaged element);

        /// <summary>
        /// Takes a change of a class structure to the followers when the class is a blueprint -
        /// for a writer that does not go through the structure managers, such as the workflow
        /// editor saving a graph.
        /// </summary>
        /// <param name="classId">The class whose structure changed.</param>
        void OnStructureChanged(Guid? classId);

        /// <summary>
        /// Subscribes to the structure managers, so a change of a blueprint reaches its followers.
        /// Called once at start-up; calling it again does nothing.
        /// </summary>
        void Connect();
    }

    /// <summary>
    /// What one synchronization of a follower changed.
    /// </summary>
    public sealed class BlueprintSyncResult
    {
        /// <summary>
        /// Gets or sets how many elements were added to the follower.
        /// </summary>
        public int Added { get; set; }

        /// <summary>
        /// Gets or sets how many managed copies were updated.
        /// </summary>
        public int Updated { get; set; }

        /// <summary>
        /// Gets or sets how many managed copies were retired because the blueprint dropped them.
        /// </summary>
        public int Retired { get; set; }
    }
}
