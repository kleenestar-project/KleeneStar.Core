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

namespace KleeneStar.Core.WWW.Issue._objectkey_
{
    /// <summary>
    /// Provides functionality for a object view.
    /// </summary>
    [WebIcon<IconObject>]
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
        /// The object manager used to retrieve object information. Cannot be null.
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

            // a link that addressed the wrong route (e.g. /issue for a document) is
            // redirected to the detail view matching the object's actual kind
            // ... and a former key (the object moved to another workspace) leads to the current one
            if (@object is not null &&
                (!string.Equals(@object.Kind, Model.Entities.ObjectKind.Issue, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(@object.Key, objectParameter?.Value, StringComparison.OrdinalIgnoreCase)))
            {
                throw new RedirectException(CoreHub.ObjectKindManager.ResolveDetailUri(@object));
            }

            // record the visit so this object surfaces at the top of the object dropdown's
            // "recently used" list; an object detail page is also a subpage of its workspace,
            // so the workspace recency is advanced too
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
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Issues._workspacekey_.Index>()
                        .BindParameters(new WorkspaceKeyParameter(@object?.Workspace?.Key))
                })
                .Concat(new ObjectKeyUriPathSegmentVariable<ObjectKeyParameter>()
                {
                    Value = @object?.Key,
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>()
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
