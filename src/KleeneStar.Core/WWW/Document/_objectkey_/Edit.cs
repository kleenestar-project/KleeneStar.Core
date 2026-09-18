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

namespace KleeneStar.Core.WWW.Document._objectkey_
{
    /// <summary>
    /// The editing route of a single document: the prose editor, opened with the page. The URL is
    /// <c>/document/{objectkey}/edit</c>.
    /// </summary>
    /// <remarks>
    /// The editor is the framework's <c>ControlDataModalEditor</c>, contributed by
    /// <see cref="WebFragment.Object.ObjectProseEditorPageFragment"/>. It is the same dialog the
    /// reading view opens, configured to show itself rather than to wait for a trigger - which is
    /// what this route is for: an editor that can be linked to, from a notification or a
    /// bookmark. Publishing and abandoning behave exactly as they do there.
    /// <para>
    /// The <c>{objectkey}</c> segment is declared by the sibling <see cref="Index"/> page, so
    /// this sibling must NOT redeclare it. As on the reading view, an object whose kind is not
    /// <see cref="Model.Entities.ObjectKind.Document"/> is redirected to the detail view matching
    /// its own kind.
    /// </para>
    /// </remarks>
    [WebIcon<IconPen>]
    [Title("kleenestar.core:object.kind.document.edit.title")]
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class Edit : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="objectManager">
        /// The object manager used to retrieve the addressed document. Cannot be null.
        /// </param>
        public Edit(IObjectManager objectManager)
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

            if (@object is not null &&
                !string.Equals(@object.Kind, Model.Entities.ObjectKind.Document, StringComparison.OrdinalIgnoreCase))
            {
                throw new RedirectException(ObjectKindCatalog.ResolveDetailUri(@object));
            }

            // the breadcrumb mirrors the reading view; its object crumb links back to the
            // reading view so the user can leave the editor without saving
            var uri = renderContext.PageContext.ApplicationContext.Route
                .Concat(new WorkspaceKeyUriPathSegmentVariable<WorkspaceKeyParameter>()
                {
                    Value = @object?.Workspace?.Key,
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Documents._workspacekey_.Index>()
                        .BindParameters(new WorkspaceKeyParameter(@object?.Workspace?.Key))
                })
                .Concat(new ObjectKeyUriPathSegmentVariable<ObjectKeyParameter>()
                {
                    Value = @object?.Key,
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Document._objectkey_.Index>()
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
