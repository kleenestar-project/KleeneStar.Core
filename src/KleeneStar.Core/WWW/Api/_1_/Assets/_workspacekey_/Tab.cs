using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebParameter;
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

namespace KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_
{
    /// <summary>
    /// REST API endpoint that returns and mutates the tab views (<see cref="ObjectView"/>)
    /// configured for a workspace's asset overview. Mirrors the issue tab endpoint but is
    /// scoped to the asset kind, so the asset and issue overviews keep independent tab
    /// sets. The asset overview offers the same layouts as the issue overview except the
    /// two Scrum boards.
    /// </summary>
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
                ObjectViewType.Dashboard => CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_.Dashboard>(),
                ObjectViewType.Kanban => CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_.Kanban>(),
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
        /// Returns the persisted asset views for the workspace identified by the route's
        /// workspace key, in display order.
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
                .GetViewsForWorkspace(workspace.Id, Model.Entities.ObjectKind.Asset)
                .Where(x => x.State == ObjectViewState.Active);

            foreach (var view in views)
            {
                yield return new RestApiTabView
                {
                    Id = view.Id.ToString(),
                    Name = view.Name,
                    Title = view.Name,
                    Icon = (view.ViewType.Icon() as WebExpress.WebUI.WebIcon.Icon)?.Class,
                    TemplateId = ResolveTemplateId(view.ViewType),
                    Uri = ResolveContentUri(view.ViewType, request)?.ToString(),
                    Binding = new Dictionary<string, object> { ["boardservice"] = BoardService(view, request) }
                };
            }
        }

        /// <summary>
        /// Persists a new asset <see cref="ObjectView"/> for the workspace, defaulting to
        /// the view type whose <see cref="ObjectViewTypeExtensions.TemplateId"/> matches
        /// the supplied <paramref name="templateId"/>.
        /// </summary>
        protected override IRestApiTabView CreateView(IQueryContext context, IRequest request, string templateId)
        {
            var workspaceKey = request?.GetParameter<WorkspaceKeyParameter>()?.Value;
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(workspaceKey);

            if (workspace is null)
            {
                return null;
            }

            var viewType = ResolveViewType(templateId);
            var existing = CoreHub.ObjectViewManager.GetViewsForWorkspace(workspace.Id, Model.Entities.ObjectKind.Asset).ToList();
            var name = BuildUniqueName(existing, viewType.ToString());

            var view = new ObjectView
            {
                Id = Guid.NewGuid(),
                Name = name,
                Kind = Model.Entities.ObjectKind.Asset,
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
                Icon = (viewType.Icon() as WebExpress.WebUI.WebIcon.Icon)?.Class,
                TemplateId = ResolveTemplateId(viewType),
                Uri = ResolveContentUri(viewType, request)?.ToString(),
                Binding = new Dictionary<string, object> { ["boardservice"] = BoardService(view, request) }
            };
        }

        /// <summary>
        /// Maps a view type to the id of the <em>asset</em> tab-template fragment that
        /// renders it. The asset overview embeds its own templates, so the mapping is asked
        /// for the asset kind rather than for the issue one.
        /// </summary>
        /// <param name="type">The view type.</param>
        /// <returns>The asset tab-template fragment id.</returns>
        private static string ResolveTemplateId(ObjectViewType type)
        {
            return ObjectViewTemplate.ResolveTemplateId(type, Model.Entities.ObjectKind.Asset);
        }

        /// <summary>
        /// Removes the <see cref="ObjectView"/> identified by <paramref name="viewId"/>.
        /// </summary>
        protected override bool RemoveView(string viewId)
        {
            if (!Guid.TryParse(viewId, out var guid))
            {
                return false;
            }

            var view = CoreHub.ObjectViewManager.GetObjectView(guid);

            if (view is null)
            {
                return false;
            }

            CoreHub.ObjectViewManager.RemoveObjectView(view);

            return true;
        }

        /// <summary>
        /// Maps a tab-template id back to the corresponding <see cref="ObjectViewType"/>.
        /// Falls back to <see cref="ObjectViewType.Table"/> when the id is unknown. The
        /// asset picker never offers the Scrum templates, so those types never reach here.
        /// </summary>
        private static ObjectViewType ResolveViewType(string templateId)
        {
            return ObjectViewTemplate.ResolveViewType(templateId, Model.Entities.ObjectKind.Asset)
                ?? ObjectViewType.Table;
        }

        /// <summary>
        /// Resolves the content REST endpoint URI for the given view type and binds the
        /// current request's workspace key. The asset layouts point at the asset content
        /// endpoints; the curated <see cref="ObjectViewType.Assets"/> view reuses the
        /// asset table endpoint.
        /// </summary>
        private static IUri ResolveContentUri(ObjectViewType type, IRequest request)
        {
            var uri = type switch
            {
                ObjectViewType.Table => CoreHub.GetUri<Table>(),
                ObjectViewType.List => CoreHub.GetUri<List>(),
                ObjectViewType.Dashboard => CoreHub.GetUri<Dashboard>(),
                ObjectViewType.Kanban => CoreHub.GetUri<Kanban>(),
                ObjectViewType.Assets => CoreHub.GetUri<Table>(),
                _ => null
            };

            return uri?.BindParameters(request);
        }

        /// <summary>
        /// Returns a name based on <paramref name="seed"/> that doesn't collide with the
        /// existing asset views in the workspace.
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
