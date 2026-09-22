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
        /// Returns the child templates of a template, in their defined order.
        /// </summary>
        /// <remarks>
        /// The children compose the template: creating an object from the parent also creates one
        /// object per active child. Archived children are omitted, so retiring a child stops it
        /// from being created without breaking the templates that already reference it.
        /// </remarks>
        /// <param name="templateId">The id of the parent template.</param>
        /// <returns>The active child templates. The collection may be empty.</returns>
        IEnumerable<Template> GetChildTemplates(Guid templateId);

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
        /// Determines whether pointing a template's parent reference at a candidate would close a
        /// cycle, so the caller can reject the change before it is persisted.
        /// </summary>
        /// <param name="templateId">The template that would carry the reference.</param>
        /// <param name="candidateId">The template it would point at.</param>
        /// <returns>True when the reference would be circular; otherwise false.</returns>
        bool WouldFormCycle(Guid templateId, Guid candidateId);

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