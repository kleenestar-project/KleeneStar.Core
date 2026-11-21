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
        /// Returns the presets a template stores, without the description the template
        /// contributes on its own.
        /// </summary>
        /// <param name="templateId">The id of the template whose presets are read.</param>
        /// <returns>The stored presets, keyed by field name. The map may be empty.</returns>
        public IReadOnlyDictionary<string, string> GetStoredPresets(Guid templateId)
        {
            return ParsePresets(GetTemplate(templateId)?.Presets);
        }

        /// <summary>
        /// Returns the classes a template serves: the class it is bound to first, then the
        /// further classes.
        /// </summary>
        /// <param name="templateId">The template.</param>
        /// <returns>The class ids; empty for an unknown template.</returns>
        public IReadOnlyList<Guid> GetClassIds(Guid templateId)
        {
            var template = GetTemplate(templateId);

            return template is null
                ? []
                : GetClassIds([template]).TryGetValue(template.Id, out var ids) ? ids : [template.ClassId];
        }

        /// <summary>
        /// Returns the classes the given templates serve, read in one query.
        /// </summary>
        /// <param name="templates">The templates.</param>
        /// <returns>The class ids per template id, the class it is bound to first.</returns>
        public IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> GetClassIds(IEnumerable<Template> templates)
        {
            var list = (templates ?? []).Where(x => x is not null).DistinctBy(x => x.Id).ToList();

            if (list.Count == 0)
            {
                return new Dictionary<Guid, IReadOnlyList<Guid>>();
            }

            var further = ModelHub.GetTemplateClassIds(list.Select(x => x.Id));

            return list.ToDictionary
            (
                x => x.Id,
                x => (IReadOnlyList<Guid>)[x.ClassId, .. (further.TryGetValue(x.Id, out var ids) ? ids : []).Where(id => id != x.ClassId)]
            );
        }

        /// <summary>
        /// Returns the templates that serve a class as a further class.
        /// </summary>
        /// <param name="classId">The class.</param>
        /// <returns>The template ids.</returns>
        public IReadOnlyList<Guid> GetFurtherTemplateIds(Guid classId)
        {
            return classId == Guid.Empty ? [] : ModelHub.GetTemplateIdsServingClass(classId);
        }

        /// <summary>
        /// Replaces the further classes of a template.
        /// </summary>
        /// <param name="templateId">The template.</param>
        /// <param name="classIds">The further classes.</param>
        /// <returns>The current instance for method chaining.</returns>
        public ITemplateManager SetFurtherClasses(Guid templateId, IEnumerable<Guid> classIds)
        {
            ModelHub.SetTemplateClasses(templateId, classIds);

            return this;
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