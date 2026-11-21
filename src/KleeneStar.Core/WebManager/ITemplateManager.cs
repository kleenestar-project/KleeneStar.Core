using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing templates, including adding, retrieving, and removing, as well as
    /// handling template-related events.
    /// </summary>
    public interface ITemplateManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when a template is added.
        /// </summary>
        event EventHandler<Template> TemplateAdded;

        /// <summary>
        /// An event that fires when a template is updated.
        /// </summary>
        event EventHandler<Template> TemplateUpdated;

        /// <summary>
        /// An event that fires when a template is removed.
        /// </summary>
        event EventHandler<Template> TemplateRemoved;

        /// <summary>
        /// Returns a template based on its id.
        /// </summary>
        /// <param name="templateId">The id of the template.</param>
        /// <returns>The template.</returns>
        Template GetTemplate(Guid templateId);

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
        IEnumerable<Template> GetTemplates(IQuery<Template> query);

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
        IEnumerable<Template> GetTemplates(IQuery<Template> query, IQueryContext context);

        /// <summary>
        /// Returns the field presets a template applies.
        /// </summary>
        /// <remarks>
        /// The keys are field names as the object create endpoint expects them. The description
        /// of the template is the text an object created from it starts with and is answered as
        /// the <c>Description</c> preset unless the template carries one of its own.
        /// </remarks>
        /// <param name="templateId">The id of the template whose presets are read.</param>
        /// <returns>The presets, keyed by field name. The map may be empty.</returns>
        IReadOnlyDictionary<string, string> GetPresets(Guid templateId);

        /// <summary>
        /// Returns the presets a template stores, without the description the template
        /// contributes on its own (<see cref="GetPresets"/>).
        /// </summary>
        /// <param name="templateId">The id of the template whose presets are read.</param>
        /// <returns>The stored presets, keyed by field name. The map may be empty.</returns>
        IReadOnlyDictionary<string, string> GetStoredPresets(Guid templateId);

        /// <summary>
        /// Returns the classes a template serves: the class it is bound to first, then the
        /// further classes in the order they were assigned.
        /// </summary>
        /// <param name="templateId">The template.</param>
        /// <returns>The class ids; empty for an unknown template.</returns>
        IReadOnlyList<Guid> GetClassIds(Guid templateId);

        /// <summary>
        /// Returns the classes the given templates serve, read in one query.
        /// </summary>
        /// <param name="templates">The templates.</param>
        /// <returns>
        /// The class ids per template id: the class it is bound to first, then the further
        /// classes.
        /// </returns>
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> GetClassIds(IEnumerable<Template> templates);

        /// <summary>
        /// Returns the templates that serve a class as a further class, beside the class they
        /// are bound to.
        /// </summary>
        /// <param name="classId">The class.</param>
        /// <returns>The template ids.</returns>
        IReadOnlyList<Guid> GetFurtherTemplateIds(Guid classId);

        /// <summary>
        /// Replaces the further classes of a template. The class the template is bound to is
        /// never stored as a further class.
        /// </summary>
        /// <param name="templateId">The template.</param>
        /// <param name="classIds">The further classes.</param>
        /// <returns>The current instance for method chaining.</returns>
        ITemplateManager SetFurtherClasses(Guid templateId, IEnumerable<Guid> classIds);

        /// <summary>
        /// Adds a new template to the collection.
        /// </summary>
        /// <param name="templateEntry">
        /// The template to add. Cannot be null.
        /// </param>
        /// <returns>The manager instance to allow method chaining.</returns>
        ITemplateManager AddTemplate(Template templateEntry);

        /// <summary>
        /// Updates the properties of an existing template.
        /// </summary>
        /// <param name="templateEntry">
        /// The template with updated properties. Cannot be null.
        /// </param>
        /// <returns>The manager instance to allow method chaining.</returns>
        ITemplateManager UpdateTemplate(Template templateEntry);

        /// <summary>
        /// Removes the specified template from the collection.
        /// </summary>
        /// <param name="templateEntry">
        /// The template to remove. Cannot be null.
        /// </param>
        /// <returns>The manager instance to allow method chaining.</returns>
        ITemplateManager RemoveTemplate(Template templateEntry);
    }
}