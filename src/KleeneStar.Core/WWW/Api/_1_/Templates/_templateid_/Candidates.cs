using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using WebExpress.WebIndex.Wql;

namespace KleeneStar.Core.WWW.Api._1_.Templates._templateid_
{
    /// <summary>
    /// Provides the templates an existing template may point its parent reference at: the
    /// templates of the same workspace, without the template itself and without anything that
    /// would close a cycle.
    /// </summary>
    /// <remarks>
    /// Offering only valid targets is what keeps the edit form from producing a choice the create
    /// endpoint then has to reject.
    /// </remarks>
    [Title("Template hierarchy selection")]
    [Cache]
    public sealed class Candidates : RestApiSelection<Model.Entities.Template>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Candidates()
        {
        }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Returns how a WQL statement is turned into a query: translatable, because the
        /// query runs against the database.
        /// </summary>
        protected override WqlQueryMode QueryMode => WqlQueryMode.Translatable;

        /// <summary>
        /// Applies a name search to the template query.
        /// </summary>
        protected override IQuery<Model.Entities.Template> Filter(string filter, IQuery<Model.Entities.Template> query, IRequest request)
        {
            return string.IsNullOrWhiteSpace(filter) || filter == "null"
                ? query
                : query.WhereContainsIgnoreCase(x => x.Name, filter);
        }

        /// <summary>
        /// Retrieves the templates the addressed template may reference.
        /// </summary>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<Model.Entities.Template> query, IQueryContext context, IRequest request)
        {
            var list = new List<RestApiSelectionItem>()
            {
                new() { Id = Guid.Empty, Text = I18N.Translate(request, "kleenestar.core:template.property.none") }
            };

            var parameter = request.GetParameter<TemplateIdParameter>();
            var templateId = Guid.TryParse(parameter?.Value, out var parsed) ? parsed : Guid.Empty;
            var template = CoreHub.TemplateManager.GetTemplate(templateId);
            var workspaceId = template?.Class?.WorkspaceId;

            if (workspaceId is null)
            {
                return list.AsQueryable();
            }

            // a template of another workspace is offered where the caller administers it as
            // well: a work structure may reach into other departments with the consent of both
            // sides, which the endpoint checks again when the reference is saved
            list.AddRange(CoreHub.TemplateManager.GetTemplates(query, context)
                .Where(x => x.Id != templateId)
                .Where(x => x.Class?.WorkspaceId == workspaceId.Value
                    || global::KleeneStar.Core.WebRestApi.ContentAuthorization.MayAdminister(x.ClassId, request))
                .Where(x => !CoreHub.TemplateManager.WouldFormCycle(templateId, x.Id))
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new RestApiSelectionItem()
                {
                    Id = x.Id,
                    Text = x.Name
                }));

            return list.AsQueryable();
        }
    }
}
