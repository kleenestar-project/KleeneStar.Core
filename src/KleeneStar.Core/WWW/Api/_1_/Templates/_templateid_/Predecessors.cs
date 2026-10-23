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
    /// Selection endpoint feeding the predecessor of a template: its siblings below the same
    /// parent, whose objects the same instantiation creates.
    /// </summary>
    [Title("kleenestar.core:template.predecessor.label")]
    [Cache]
    public sealed class Predecessors : RestApiSelection<Model.Entities.Template>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Predecessors()
        {
        }

        /// <inheritdoc/>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <inheritdoc/>
        protected override WqlQueryMode QueryMode => WqlQueryMode.Translatable;

        /// <inheritdoc/>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<Model.Entities.Template> query, IQueryContext context, IRequest request)
        {
            var list = new List<RestApiSelectionItem>
            {
                new() { Id = Guid.Empty, Text = I18N.Translate(request, "kleenestar.core:template.property.none") }
            };

            var parameter = request.GetParameter<TemplateIdParameter>();
            var template = Guid.TryParse(parameter?.Value, out var templateId)
                ? CoreHub.TemplateManager.GetTemplate(templateId)
                : null;

            // a template without a parent has no siblings an instantiation would create with it
            if (template?.ParentId is not Guid parentId)
            {
                return list.AsQueryable();
            }

            list.AddRange(CoreHub.TemplateManager
                .GetChildTemplates(parentId)
                .Where(x => x.Id != template.Id)
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new RestApiSelectionItem { Id = x.Id, Text = x.Name }));

            return list.AsQueryable();
        }

        /// <inheritdoc/>
        protected override IQuery<Model.Entities.Template> Filter(string filter, IQuery<Model.Entities.Template> query, IRequest request)
        {
            return string.IsNullOrWhiteSpace(filter) || filter == "null"
                ? query
                : query.WhereContainsIgnoreCase(x => x.Name, filter);
        }
    }
}
