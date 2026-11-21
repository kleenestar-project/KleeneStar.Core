using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_
{
    /// <summary>
    /// REST API endpoint that returns and mutates the tab views (<see cref="ObjectView"/>)
    /// configured for a workspace's objects index.
    /// </summary>
    /// <remarks>
    /// Each <see cref="RestApiTabView"/> returned by <see cref="RetrieveViews"/> binds an
    /// <see cref="ObjectView"/> to one of the tab template fragments registered on the
    /// objects tab via its <c>TemplateId</c> — the fragment id of the template (full type
    /// name, lower-cased, dots replaced by dashes). Table and List share the composite
    /// object view template; Dashboard, Kanban, Gantt, Scheduler and the two Scrum types
    /// each have their own.
    /// </remarks>
    [Title("kleenestar.core:object.tab.header")]
    [Cache]
    public sealed class Tab : RestApiTab<Model.Entities.Object>
    {
        /// <summary>
        /// Builds the board service address with the owning tab identifier.
        /// </summary>
        /// <param name="view">The tab whose board is addressed.</param>
        /// <param name="request">The request supplying the route parameters.</param>
        /// <returns>The board service address, or null for a tab without a board.</returns>
        private static string BoardService(ObjectView view, IRequest request)
        {
            var uri = view.ViewType switch
            {
                ObjectViewType.Dashboard => CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_.Dashboard>(),
                ObjectViewType.Kanban => CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_.Kanban>(),
                ObjectViewType.ScrumSprint or ObjectViewType.ScrumBacklog => CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_.ScrumSprintKanban>(),
                _ => null
            };

            return uri?.Add(new UriQuery("v", view.Id.ToString())).BindParameters(request).ToString();
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Tab()
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
        /// Returns the persisted object views for the workspace identified by the
        /// route's workspace key, in display order.
        /// </summary>
        protected override IEnumerable<RestApiTabView> RetrieveViews(IQueryContext context, IRequest request)
        {
            var workspaceKey = request?.GetParameter<WorkspaceKeyParameter>()?.Value;
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(workspaceKey);

            if (workspace is null)
            {
                yield break;
            }

            var views = CoreHub.ObjectViewManager
                .GetViewsForWorkspace(workspace.Id, Model.Entities.ObjectKind.Issue)
                .Where(x => x.State == ObjectViewState.Active);

            foreach (var view in views)
            {
                yield return new RestApiTabView
                {
                    Id = view.Id.ToString(),
                    Name = view.Name,
                    Title = view.Name,
                    TabColor = view.Color,
                    Icon = (view.ViewType.Icon() as WebExpress.WebUI.WebIcon.Icon)?.Class,
                    TemplateId = ObjectViewTemplate.ResolveTemplateId(view.ViewType, Model.Entities.ObjectKind.Issue),
                    Uri = ResolveContentUri(view.ViewType, request)?.ToString(),
                    Binding = BuildBinding(view, request)
                };
            }
        }

        /// <summary>
        /// Builds the binding payload of a tab: the values the tab template writes into
        /// its content controls when the client instantiates it for this view.
        /// </summary>
        /// <remarks>
        /// A tab template renders once on the server and is cloned into a pane per view,
        /// so a control inside it cannot know at render time which view it ends up
        /// serving. The issue table needs to know, because the column layout it shows is
        /// stored per view; it therefore declares a template binding for
        /// <c>issuetable</c> (see <c>IssueTabViewTableFragment</c>) that writes the value
        /// below onto the base address of its data service, and every view's table talks
        /// to its own address from then on.
        /// </remarks>
        /// <param name="view">The view the tab shows.</param>
        /// <param name="request">The request, used to bind the route parameters.</param>
        /// <returns>The binding payload.</returns>
        private static IDictionary<string, object> BuildBinding(ObjectView view, IRequest request)
        {
            var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Issues._workspacekey_.Table>()?
                .Add(new UriQuery("v", view.Id.ToString()))
                .BindParameters(request);

            return new Dictionary<string, object>
            {
                ["issuetable"] = uri?.ToString(),
                ["boardservice"] = BoardService(view, request)
            };
        }

        /// <summary>
        /// Persists a new <see cref="ObjectView"/> for the workspace, defaulting to the
        /// view type whose <see cref="ObjectViewTypeExtensions.TemplateId"/> matches the
        /// supplied <paramref name="templateId"/>.
        /// </summary>
        protected override IRestApiTabView CreateView(IQueryContext context, IRequest request, string templateId)
        {
            var workspaceKey = request?.GetParameter<WorkspaceKeyParameter>()?.Value;
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(workspaceKey);

            if (workspace is null)
            {
                return null;
            }

            ObjectViewTabs.DemandArrange(request);

            var viewType = ResolveViewType(templateId);
            var existing = CoreHub.ObjectViewManager.GetViewsForWorkspace(workspace.Id, Model.Entities.ObjectKind.Issue).ToList();
            var name = BuildUniqueName(existing, viewType.ToString());

            var view = new ObjectView
            {
                Id = Guid.NewGuid(),
                Name = name,
                Kind = Model.Entities.ObjectKind.Issue,
                ViewType = viewType,
                Order = existing.Count == 0 ? 0 : existing.Max(x => x.Order) + 1,
                State = ObjectViewState.Active,
                WorkspaceId = workspace.Id,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };

            CoreHub.ObjectViewManager.AddObjectView(view);

            return new RestApiTabView
            {
                Id = view.Id.ToString(),
                Name = view.Name,
                Title = view.Name,
                TabColor = view.Color,
                Icon = (viewType.Icon() as WebExpress.WebUI.WebIcon.Icon)?.Class,
                TemplateId = ObjectViewTemplate.ResolveTemplateId(viewType, Model.Entities.ObjectKind.Issue),
                Uri = ResolveContentUri(viewType, request)?.ToString(),
                Binding = BuildBinding(view, request)
            };
        }

        /// <summary>
        /// Removes the <see cref="ObjectView"/> identified by <paramref name="viewId"/>, a tab of
        /// the workspace the route names, for a caller who may change its content.
        /// </summary>
        protected override bool RemoveView(string viewId)
        {
            return ObjectViewTabs.Remove(viewId, Model.Entities.ObjectKind.Issue, WebExpress.WebCore.WebEx.CurrentRequest);
        }

        /// <summary>
        /// Persists the order the tabs were dragged into.
        /// </summary>
        /// <param name="order">The tab ids in their new order.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the order was applied.</returns>
        protected override bool ReorderViews(IReadOnlyList<string> order, IQueryContext context, IRequest request)
        {
            return ObjectViewTabs.Reorder(order, Model.Entities.ObjectKind.Issue, request);
        }

        /// <summary>
        /// Gets the maximum length of a tab label - the length of the name column.
        /// </summary>
        protected override int MaxLabelLength => ObjectViewTabs.MaxNameLength;

        /// <summary>
        /// Renames a tab from its menu.
        /// </summary>
        /// <param name="viewId">The id of the tab.</param>
        /// <param name="label">The new label, trimmed and not empty.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the tab was renamed.</returns>
        protected override bool RenameView(string viewId, string label, IQueryContext context, IRequest request)
        {
            return ObjectViewTabs.Rename(viewId, label, Model.Entities.ObjectKind.Issue, request);
        }

        /// <summary>
        /// Sets or clears the color of a tab from its menu.
        /// </summary>
        /// <param name="viewId">The id of the tab.</param>
        /// <param name="color">The color as a <c>#rrggbb</c> value, or <see langword="null"/>.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the tab was changed.</returns>
        protected override bool RecolorView(string viewId, string color, IQueryContext context, IRequest request)
        {
            return ObjectViewTabs.Recolor(viewId, color, Model.Entities.ObjectKind.Issue, request);
        }

        /// <summary>
        /// Maps a tab-template id back to the corresponding <see cref="ObjectViewType"/>.
        /// Falls back to <see cref="ObjectViewType.Table"/> when the id is unknown.
        /// </summary>
        private static ObjectViewType ResolveViewType(string templateId)
        {
            return ObjectViewTemplate.ResolveViewType(templateId, Model.Entities.ObjectKind.Issue)
                ?? ObjectViewType.Table;
        }

        /// <summary>
        /// Resolves the content REST endpoint URI for the given view type and binds the
        /// current request's workspace key. The returned URI is sent in
        /// <see cref="RestApiTabView.Uri"/> and the client-side <c>BindTemplate</c> on each
        /// tab-template fragment routes it into the <c>data-uri</c> of the active tab's
        /// inner content control.
        /// </summary>
        private static IUri ResolveContentUri(ObjectViewType type, IRequest request)
        {
            var uri = type switch
            {
                ObjectViewType.Table => CoreHub.GetUri<Table>(),
                ObjectViewType.List => CoreHub.GetUri<List>(),
                ObjectViewType.Dashboard => CoreHub.GetUri<Dashboard>(),
                ObjectViewType.Kanban => CoreHub.GetUri<Kanban>(),
                ObjectViewType.ScrumSprint => CoreHub.GetUri<ScrumSprint>(),
                ObjectViewType.ScrumBacklog => CoreHub.GetUri<ScrumBacklog>(),
                ObjectViewType.Issues => CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Issues._workspacekey_.Table>(),
                ObjectViewType.Gantt => CoreHub.GetUri<Gantt>(),
                ObjectViewType.Scheduler => CoreHub.GetUri<Scheduler>(),
                _ => null
            };

            return uri?.BindParameters(request);
        }

        /// <summary>
        /// Returns a name based on <paramref name="seed"/> that doesn't collide with the
        /// existing views in the workspace.
        /// </summary>
        private static string BuildUniqueName(IEnumerable<ObjectView> existing, string seed)
        {
            var taken = existing
                .Select(x => x.Name ?? string.Empty)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!taken.Contains(seed))
            {
                return seed;
            }

            for (var i = 2; i < 1000; i++)
            {
                var candidate = $"{seed} ({i})";
                if (!taken.Contains(candidate))
                {
                    return candidate;
                }
            }

            return $"{seed} ({Guid.NewGuid():N})";
        }
    }
}
