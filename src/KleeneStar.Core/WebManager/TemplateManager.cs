using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing templates, including adding, retrieving, and removing, as well as
    /// handling template-related events.
    /// </summary>
    public sealed class TemplateManager : ITemplateManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// An event that fires when a template is added.
        /// </summary>
        public event EventHandler<Template> TemplateAdded;

        /// <summary>
        /// An event that fires when a template is updated.
        /// </summary>
        public event EventHandler<Template> TemplateUpdated;

        /// <summary>
        /// An event that fires when a template is removed.
        /// </summary>
        public event EventHandler<Template> TemplateRemoved;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private TemplateManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns a template based on its id.
        /// </summary>
        /// <param name="templateId">The id of the template.</param>
        /// <returns>The template.</returns>
        public Template GetTemplate(Guid templateId)
        {
            var query = new Query<Template>()
                .Where(x => x.Id == templateId)
                .WithPaging(0, 1);

            return ModelHub.GetTemplates(query)
                .FirstOrDefault();
        }

        /// <summary>
        /// Retrieves a collection of templates that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned templates. Must not be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of templates that match the given predicate. If no template 
        /// match, the collection will be empty.
        /// </returns>
        public IEnumerable<Template> GetTemplates(IQuery<Template> query)
        {
            return ModelHub.GetTemplates(query);
        }

        /// <summary>
        /// Retrieves a collection of templates that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned templates. Must not be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints 
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of templates that match the given predicate. If no template 
        /// match, the collection will be empty.
        /// </returns>
        public IEnumerable<Template> GetTemplates(IQuery<Template> query, IQueryContext context)
        {
            if (context is KleeneStarDbContext db)
            {
                return ModelHub.GetTemplates(query, db);
            }

            return [];
        }

        /// <summary>
        /// Returns the child templates of a template, in their defined order.
        /// </summary>
        /// <param name="templateId">The id of the parent template.</param>
        /// <returns>The active child templates. The collection may be empty.</returns>
        public IEnumerable<Template> GetChildTemplates(Guid templateId)
        {
            if (templateId == Guid.Empty)
            {
                return [];
            }

            var query = new Query<Template>()
                .Where(x => x.ParentId == templateId && x.State == TemplateState.Active)
                .OrderByAsc(x => x.Order);

            return ModelHub.GetTemplates(query);
        }

        /// <summary>
        /// Returns the field presets a template applies.
        /// </summary>
        /// <remarks>
        /// The description of a template is the text an object created from it starts with:
        /// the template dialog writes it with the same editor the object description uses, and
        /// a child of a composite template has always been described by it. It therefore stands
        /// in for a <c>Description</c> preset the template does not carry explicitly - one the
        /// serialized presets name wins, so a template can say one thing on its card and start
        /// the object with another.
        /// </remarks>
        /// <param name="templateId">The id of the template whose presets are read.</param>
        /// <returns>The presets, keyed by field name. The map may be empty.</returns>
        public IReadOnlyDictionary<string, string> GetPresets(Guid templateId)
        {
            var template = GetTemplate(templateId);
            var presets = ParsePresets(template?.Presets);

            if (!string.IsNullOrWhiteSpace(template?.Description)
                && !presets.ContainsKey(nameof(Model.Entities.Object.Description)))
            {
                presets[nameof(Model.Entities.Object.Description)] = template.Description;
            }

            return presets;
        }

        /// <summary>
        /// Determines whether pointing a template's parent reference at a candidate would close a
        /// cycle.
        /// </summary>
        /// <param name="templateId">The template that would carry the reference.</param>
        /// <param name="candidateId">The template it would point at.</param>
        /// <returns>True when the reference would be circular; otherwise false.</returns>
        public bool WouldFormCycle(Guid templateId, Guid candidateId)
        {
            if (templateId == Guid.Empty || candidateId == Guid.Empty)
            {
                return false;
            }

            // pointing at itself is the shortest cycle there is
            if (templateId == candidateId)
            {
                return true;
            }

            var visited = new HashSet<Guid> { candidateId };
            var cursor = GetTemplate(candidateId);

            // walking up from the candidate must not arrive back at the template
            while (cursor is not null)
            {
                var next = cursor.ParentId;

                if (!next.HasValue)
                {
                    return false;
                }

                if (next.Value == templateId)
                {
                    return true;
                }

                if (!visited.Add(next.Value))
                {
                    // the candidate already sits in a cycle of its own; it cannot be reached
                    // from the template, so the new reference does not add one
                    return false;
                }

                cursor = GetTemplate(next.Value);
            }

            return false;
        }

        /// <summary>
        /// Reads a template's serialized presets into a field-name to value map. A payload that is
        /// absent or not a JSON object yields an empty map, so malformed data disables the presets
        /// of that template instead of failing object creation.
        /// </summary>
        /// <param name="presets">The serialized presets.</param>
        /// <returns>The parsed presets. The map may be empty.</returns>
        private static Dictionary<string, string> ParsePresets(string presets)
        {
            if (string.IsNullOrWhiteSpace(presets))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(presets);
                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var entry in parsed ?? [])
                {
                    result[entry.Key] = entry.Value.ValueKind == JsonValueKind.String
                        ? entry.Value.GetString()
                        : entry.Value.ToString();
                }

                return result;
            }
            catch (JsonException)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Adds a new template to the collection.
        /// </summary>
        /// <param name="templateEntry">
        /// The template to add. Cannot be null.
        /// </param>
        /// <returns>The manager instance to allow method chaining.</returns>
        public ITemplateManager AddTemplate(Template templateEntry)
        {
            ModelHub.Add(templateEntry);

            TemplateAdded?.Invoke(this, templateEntry);

            return this;
        }

        /// <summary>
        /// Updates the properties of an existing template.
        /// </summary>
        /// <param name="templateEntry">
        /// The template with updated properties. Cannot be null.
        /// </param>
        /// <returns>The manager instance to allow method chaining.</returns>
        public ITemplateManager UpdateTemplate(Template templateEntry)
        {
            ModelHub.Update(templateEntry);

            TemplateUpdated?.Invoke(this, templateEntry);

            return this;
        }

        /// <summary>
        /// Removes the specified template from the collection.
        /// </summary>
        /// <param name="templateEntry">
        /// The template to remove. Cannot be null.
        /// </param>
        /// <returns>The manager instance to allow method chaining.</returns>
        public ITemplateManager RemoveTemplate(Template templateEntry)
        {
            ModelHub.Remove(templateEntry);

            TemplateRemoved?.Invoke(this, templateEntry);

            return this;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, 
        /// or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // Add disposal logic if necessary
        }
    }
}