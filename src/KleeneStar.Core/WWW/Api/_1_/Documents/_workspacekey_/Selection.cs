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

namespace KleeneStar.Core.WWW.Api._1_.Documents._workspacekey_
{
    /// <summary>
    /// Offers the documents of the addressed workspace for selection. It backs the home-page
    /// picker of the document overview, which is the only question that needs this list: which
    /// of these pages does the overview open on.
    /// </summary>
    /// <remarks>
    /// It is scoped to one workspace, unlike the header's document dropdown
    /// (<see cref="Api._1_.Documents.Dropdown"/>), which searches every workspace the caller can
    /// see. A home page can only be one of this workspace's own documents, so offering more than
    /// those would offer a choice the write side refuses.
    /// <para>
    /// The list leads with an entry standing for "no choice", which clears the setting and
    /// returns the overview to the first root of the page tree. It carries the empty guid,
    /// because that is what the form binder reads as "clear this property".
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:object.kind.documents.label")]
    [Cache]
    public sealed class Selection : RestApiSelection<Model.Entities.Object>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Selection()
        {
        }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        /// <returns>The query context.</returns>
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
        /// Narrows the offered documents by what was typed into the selection.
        /// </summary>
        /// <param name="filter">The typed text.</param>
        /// <param name="query">The query being built.</param>
        /// <param name="request">The request providing the operational context.</param>
        /// <returns>The narrowed query.</returns>
        protected override IQuery<Model.Entities.Object> Filter(string filter, IQuery<Model.Entities.Object> query, IRequest request)
        {
            return string.IsNullOrWhiteSpace(filter) || filter == "null"
                ? query
                : query.WhereContainsIgnoreCase(x => x.Summary, filter);
        }

        /// <summary>
        /// Retrieves the documents of the workspace addressed by the route, by title, led by the
        /// entry standing for "no choice".
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request whose route names the workspace.</param>
        /// <returns>The selectable items.</returns>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            var list = new List<RestApiSelectionItem>()
            {
                new() { Id = Guid.Empty, Text = I18N.Translate(request, "kleenestar.core:workspace.home.automatic") }
            };

            var key = request.GetParameter<WorkspaceKeyParameter>();
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(key?.Value);

            if (workspace is null)
            {
                return list.AsQueryable();
            }

            query = query
                .WhereEquals(x => x.WorkspaceId, workspace.Id)
                .WhereEquals(x => x.Kind, Model.Entities.ObjectKind.Document);

            list.AddRange(CoreHub.ObjectManager.GetObjects(query, context)
                .OrderBy(x => x.Summary, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new RestApiSelectionItem()
                {
                    Id = x.Id,
                    Text = x.Summary
                }));

            return list.AsQueryable();
        }
    }
}
