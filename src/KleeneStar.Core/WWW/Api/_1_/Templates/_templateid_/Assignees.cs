using KleeneStar.Model;
using KleeneStar.Model.Entities;
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
    /// Selection endpoint feeding the assignee of a template: the active operator-side accounts
    /// an object created from it can be handed to.
    /// </summary>
    /// <remarks>
    /// A template assigns the work it creates to the service side; a customer account is not
    /// somebody a work structure hands a task to, so accounts with a tenant are not offered.
    /// </remarks>
    [Title("kleenestar.core:template.assignee.label")]
    [Cache]
    public sealed class Assignees : RestApiSelection<Identity>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Assignees()
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
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<Identity> query, IQueryContext context, IRequest request)
        {
            var list = new List<RestApiSelectionItem>
            {
                new() { Id = Guid.Empty, Text = I18N.Translate(request, "kleenestar.core:template.property.none") }
            };

            list.AddRange(CoreHub.IdentityManager
                .GetIdentities(query, context)
                .Where(x => x.State == IdentityState.Active && x.TenantId is null)
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new RestApiSelectionItem
                {
                    Id = x.Id,
                    Text = string.IsNullOrWhiteSpace(x.Position) ? x.Name : $"{x.Name} · {x.Position}"
                }));

            return list.AsQueryable();
        }

        /// <inheritdoc/>
        protected override IQuery<Identity> Filter(string filter, IQuery<Identity> query, IRequest request)
        {
            return string.IsNullOrWhiteSpace(filter) || filter == "null"
                ? query
                : query.WhereContainsIgnoreCase(x => x.Name, filter);
        }
    }
}
