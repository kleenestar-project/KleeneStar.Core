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
    /// Manages the template hierarchies (<see cref="ITemplateHierarchyManager"/>).
    /// </summary>
    public sealed class TemplateHierarchyManager : ITemplateHierarchyManager
    {
        /// <summary>
        /// An event that fires when a hierarchy is added.
        /// </summary>
        public event EventHandler<TemplateHierarchy> HierarchyAdded;

        /// <summary>
        /// An event that fires when a hierarchy or its nodes are updated.
        /// </summary>
        public event EventHandler<TemplateHierarchy> HierarchyUpdated;

        /// <summary>
        /// An event that fires when a hierarchy is removed.
        /// </summary>
        public event EventHandler<TemplateHierarchy> HierarchyRemoved;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Signature of the component manager.")]
        private TemplateHierarchyManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
        }

        /// <summary>
        /// Returns a hierarchy by its id.
        /// </summary>
        /// <param name="hierarchyId">The id.</param>
        /// <returns>The hierarchy, or null.</returns>
        public TemplateHierarchy GetHierarchy(Guid hierarchyId)
        {
            if (hierarchyId == Guid.Empty)
            {
                return null;
            }

            return ModelHub.GetTemplateHierarchies(new Query<TemplateHierarchy>()
                .Where(x => x.Id == hierarchyId)
                .WithPaging(0, 1))
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns the hierarchies matching a query.
        /// </summary>
        /// <param name="query">The query. Must not be null.</param>
        /// <returns>The hierarchies.</returns>
        public IEnumerable<TemplateHierarchy> GetHierarchies(IQuery<TemplateHierarchy> query)
        {
            return ModelHub.GetTemplateHierarchies(query);
        }

        /// <summary>
        /// Returns the hierarchies matching a query within a context.
        /// </summary>
        /// <param name="query">The query. Must not be null.</param>
        /// <param name="context">The context the query is executed in.</param>
        /// <returns>The hierarchies.</returns>
        public IEnumerable<TemplateHierarchy> GetHierarchies(IQuery<TemplateHierarchy> query, IQueryContext context)
        {
            return context is KleeneStarDbContext db ? ModelHub.GetTemplateHierarchies(query, db) : [];
        }

        /// <summary>
        /// Returns the nodes of a hierarchy depth first.
        /// </summary>
        /// <param name="hierarchyId">The hierarchy.</param>
        /// <returns>The nodes.</returns>
        public IReadOnlyList<TemplateHierarchyNode> GetNodes(Guid hierarchyId)
        {
            return hierarchyId != Guid.Empty
                && ModelHub.GetTemplateHierarchyNodes([hierarchyId]).TryGetValue(hierarchyId, out var nodes)
                    ? nodes
                    : [];
        }

        /// <summary>
        /// Returns the nodes of several hierarchies, read in one query.
        /// </summary>
        /// <param name="hierarchyIds">The hierarchies.</param>
        /// <returns>The nodes per hierarchy id.</returns>
        public IReadOnlyDictionary<Guid, IReadOnlyList<TemplateHierarchyNode>> GetNodes(IEnumerable<Guid> hierarchyIds)
        {
            var ids = (hierarchyIds ?? []).Where(x => x != Guid.Empty).Distinct().ToList();

            return ids.Count == 0
                ? new Dictionary<Guid, IReadOnlyList<TemplateHierarchyNode>>()
                : ModelHub.GetTemplateHierarchyNodes(ids);
        }

        /// <summary>
        /// Replaces the nodes of a hierarchy.
        /// </summary>
        /// <param name="hierarchyId">The hierarchy.</param>
        /// <param name="nodes">The nodes depth first.</param>
        /// <returns>The current instance for method chaining.</returns>
        public ITemplateHierarchyManager SetNodes(Guid hierarchyId, IEnumerable<TemplateHierarchyNode> nodes)
        {
            ModelHub.SetTemplateHierarchyNodes(hierarchyId, nodes);

            if (GetHierarchy(hierarchyId) is { } hierarchy)
            {
                HierarchyUpdated?.Invoke(this, hierarchy);
            }

            return this;
        }

        /// <summary>
        /// Returns the hierarchies that use a template, as root or as a node.
        /// </summary>
        /// <param name="templateId">The template.</param>
        /// <returns>The hierarchy ids.</returns>
        public IReadOnlyList<Guid> GetHierarchyIdsUsing(Guid templateId)
        {
            return templateId == Guid.Empty ? [] : ModelHub.GetTemplateHierarchyIdsUsing(templateId);
        }

        /// <summary>
        /// Returns the class an object of a template is created in.
        /// </summary>
        /// <param name="classId">The chosen class, or null.</param>
        /// <param name="template">The template.</param>
        /// <returns>The chosen class while the template serves it, else its bound class.</returns>
        public Guid ResolveClassId(Guid? classId, Template template)
        {
            if (template is null)
            {
                return Guid.Empty;
            }

            if (classId is Guid chosen
                && chosen != Guid.Empty
                && chosen != template.ClassId
                && CoreHub.TemplateManager.GetClassIds([template]).TryGetValue(template.Id, out var served)
                && served.Contains(chosen))
            {
                return chosen;
            }

            return template.ClassId;
        }

        /// <summary>
        /// Adds a hierarchy.
        /// </summary>
        /// <param name="hierarchy">The hierarchy. Cannot be null.</param>
        /// <returns>The current instance for method chaining.</returns>
        public ITemplateHierarchyManager AddHierarchy(TemplateHierarchy hierarchy)
        {
            ModelHub.Add(hierarchy);

            HierarchyAdded?.Invoke(this, hierarchy);

            return this;
        }

        /// <summary>
        /// Updates the attributes of a hierarchy.
        /// </summary>
        /// <param name="hierarchy">The hierarchy carrying the changes. Cannot be null.</param>
        /// <returns>The current instance for method chaining.</returns>
        public ITemplateHierarchyManager UpdateHierarchy(TemplateHierarchy hierarchy)
        {
            ModelHub.Update(hierarchy);

            HierarchyUpdated?.Invoke(this, hierarchy);

            return this;
        }

        /// <summary>
        /// Removes a hierarchy with its nodes.
        /// </summary>
        /// <param name="hierarchy">The hierarchy. Cannot be null.</param>
        /// <returns>The current instance for method chaining.</returns>
        public ITemplateHierarchyManager RemoveHierarchy(TemplateHierarchy hierarchy)
        {
            ModelHub.Remove(hierarchy);

            HierarchyRemoved?.Invoke(this, hierarchy);

            return this;
        }

        /// <summary>
        /// Releases the resources of the manager.
        /// </summary>
        public void Dispose()
        {
        }
    }
}
