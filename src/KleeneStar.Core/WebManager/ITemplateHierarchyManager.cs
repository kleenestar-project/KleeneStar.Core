using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages the template hierarchies: work structures of several objects, each node a
    /// reference to an existing template, offered beside the templates when an object is created.
    /// </summary>
    public interface ITemplateHierarchyManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when a hierarchy is added.
        /// </summary>
        event EventHandler<TemplateHierarchy> HierarchyAdded;

        /// <summary>
        /// An event that fires when a hierarchy or its nodes are updated.
        /// </summary>
        event EventHandler<TemplateHierarchy> HierarchyUpdated;

        /// <summary>
        /// An event that fires when a hierarchy is removed.
        /// </summary>
        event EventHandler<TemplateHierarchy> HierarchyRemoved;

        /// <summary>
        /// Returns a hierarchy by its id.
        /// </summary>
        /// <param name="hierarchyId">The id.</param>
        /// <returns>The hierarchy, or null.</returns>
        TemplateHierarchy GetHierarchy(Guid hierarchyId);

        /// <summary>
        /// Returns the hierarchies matching a query.
        /// </summary>
        /// <param name="query">The query. Must not be null.</param>
        /// <returns>The hierarchies.</returns>
        IEnumerable<TemplateHierarchy> GetHierarchies(IQuery<TemplateHierarchy> query);

        /// <summary>
        /// Returns the hierarchies matching a query within a context.
        /// </summary>
        /// <param name="query">The query. Must not be null.</param>
        /// <param name="context">The context the query is executed in.</param>
        /// <returns>The hierarchies.</returns>
        IEnumerable<TemplateHierarchy> GetHierarchies(IQuery<TemplateHierarchy> query, IQueryContext context);

        /// <summary>
        /// Returns the nodes of a hierarchy depth first.
        /// </summary>
        /// <param name="hierarchyId">The hierarchy.</param>
        /// <returns>The nodes; empty for a hierarchy without nodes.</returns>
        IReadOnlyList<TemplateHierarchyNode> GetNodes(Guid hierarchyId);

        /// <summary>
        /// Returns the nodes of several hierarchies, read in one query.
        /// </summary>
        /// <param name="hierarchyIds">The hierarchies.</param>
        /// <returns>The nodes per hierarchy id depth first; a hierarchy without nodes has no entry.</returns>
        IReadOnlyDictionary<Guid, IReadOnlyList<TemplateHierarchyNode>> GetNodes(IEnumerable<Guid> hierarchyIds);

        /// <summary>
        /// Replaces the nodes of a hierarchy; the nodes are given depth first.
        /// </summary>
        /// <param name="hierarchyId">The hierarchy.</param>
        /// <param name="nodes">The nodes depth first.</param>
        /// <returns>The current instance for method chaining.</returns>
        ITemplateHierarchyManager SetNodes(Guid hierarchyId, IEnumerable<TemplateHierarchyNode> nodes);

        /// <summary>
        /// Returns the hierarchies that use a template, as root or as a node.
        /// </summary>
        /// <param name="templateId">The template.</param>
        /// <returns>The hierarchy ids.</returns>
        IReadOnlyList<Guid> GetHierarchyIdsUsing(Guid templateId);

        /// <summary>
        /// Returns the class an object of a template is created in: the chosen class while the
        /// template still serves it, else the class the template is bound to.
        /// </summary>
        /// <param name="classId">The chosen class, or null.</param>
        /// <param name="template">The template.</param>
        /// <returns>The class id, or the empty guid for an unknown template.</returns>
        Guid ResolveClassId(Guid? classId, Template template);

        /// <summary>
        /// Adds a hierarchy.
        /// </summary>
        /// <param name="hierarchy">The hierarchy. Cannot be null.</param>
        /// <returns>The current instance for method chaining.</returns>
        ITemplateHierarchyManager AddHierarchy(TemplateHierarchy hierarchy);

        /// <summary>
        /// Updates the attributes of a hierarchy.
        /// </summary>
        /// <param name="hierarchy">The hierarchy carrying the changes. Cannot be null.</param>
        /// <returns>The current instance for method chaining.</returns>
        ITemplateHierarchyManager UpdateHierarchy(TemplateHierarchy hierarchy);

        /// <summary>
        /// Removes a hierarchy with its nodes.
        /// </summary>
        /// <param name="hierarchy">The hierarchy. Cannot be null.</param>
        /// <returns>The current instance for method chaining.</returns>
        ITemplateHierarchyManager RemoveHierarchy(TemplateHierarchy hierarchy);
    }
}
