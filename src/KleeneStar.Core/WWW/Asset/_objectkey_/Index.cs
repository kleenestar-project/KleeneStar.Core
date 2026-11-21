using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebUri;
using System;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Asset._objectkey_
{
    /// <summary>
    /// The detail view of a single asset. It reuses the full object detail experience of
    /// the issue kind (headline, cards, metadata, property and SLA panels, comments,
    /// attachments, and the shared sidebar) — those fragments attach themselves to this
    /// page in addition to the issue detail page. The URL is <c>/asset/{objectkey}</c>.
    /// </summary>
    /// <remarks>
    /// The page is kind-aware: an object whose kind is not
    /// <see cref="Model.Entities.ObjectKind.Asset"/> is redirected to the detail view of
    /// its own kind, so a link that guessed the wrong route self-heals instead of
    /// rendering an asset chrome around, say, an issue.
    /// </remarks>
    [WebIcon<IconCubes>]
    [ObjectKeySegment]
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class Index : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="objectManager">
        /// The object manager used to retrieve the addressed asset. Cannot be null.
        /// </param>
        public Index(IObjectManager objectManager)
        {
            _objectManager = objectManager;
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var objectParameter = renderContext.Request.GetParameter<ObjectKeyParameter>();
            var @object = _objectManager.GetObjectByKey(objectParameter?.Value);

            // a link that addressed the wrong route (e.g. /asset for an issue) is
            // redirected to the detail view matching the object's actual kind
            // ... and a former key (the object moved to another workspace) leads to the current one
            if (@object is not null &&
                (!string.Equals(@object.Kind, Model.Entities.ObjectKind.Asset, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(@object.Key, objectParameter?.Value, StringComparison.OrdinalIgnoreCase)))
            {
                throw new RedirectException(CoreHub.ObjectKindManager.ResolveDetailUri(@object));
            }

            // record the visit so this asset surfaces at the top of the asset dropdown's
            // "recently used" list; a detail page is also a subpage of its workspace, so
            // the workspace recency is advanced too
            if (@object is not null)
            {
                var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request);
                CoreHub.ObjectManager.RecordVisit(ownerId, @object.Id);
                CoreHub.WorkspaceManager.RecordVisit(ownerId, @object.WorkspaceId);
            }

            var uri = renderContext.PageContext.ApplicationContext.Route
                .Concat(new WorkspaceKeyUriPathSegmentVariable<WorkspaceKeyParameter>()
                {
                    Value = @object?.Workspace?.Key,
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Assets._workspacekey_.Index>()
                        .BindParameters(new WorkspaceKeyParameter(@object?.Workspace?.Key))
                })
                .Concat(new ObjectKeyUriPathSegmentVariable<ObjectKeyParameter>()
                {
                    Value = @object?.Key,
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>()
                        .BindParameters(new ObjectKeyParameter(@object?.Key))
                })
                .ToUri()
                .BindParameters(renderContext.Request);

            visualTree.BreadcrumbUri = uri;
            visualTree.Title = @object?.Summary;
            visualTree.Content.MainPanel.Headline.Title = @object?.Summary;
        }
    }
}
