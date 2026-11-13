using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object.Documents
{
    /// <summary>
    /// The prose editor of the workspace's home page, standing on the document overview where
    /// the page is read. It renders closed and is opened by <see cref="DocumentHomeEditButtonFragment"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the prose editor of the documents (<see cref="ObjectProseEditorFragmentBase"/>:
    /// drafts, publishing in one commit, collaborative surface). The object is resolved from the
    /// workspace route instead of an object key, because the home page has no detail page.
    /// </para>
    /// <para>
    /// The draft endpoint is declared with the object key bound into its address. The framework
    /// binds the path variables of a draft address from the request; this request carries a
    /// workspace key and no object key, so the inherited declaration would leave
    /// <c>{objectkey}</c> unbound.
    /// </para>
    /// <para>
    /// Renders only when <see cref="DocumentHome.MayEdit"/> holds; the publication endpoint
    /// checks write access again.
    /// </para>
    /// </remarks>
    [Section<SectionBodySecondary>]
    [Scope<global::KleeneStar.Core.WWW.Documents._workspacekey_.Index>]
    [Cache]
    public sealed class DocumentHomeEditorFragment : ObjectProseEditorFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        public DocumentHomeEditorFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            DraftServiceFactory = renderContext =>
            {
                var home = ResolveObject(renderContext);
                var uri = home is null
                    ? null
                    : CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Drafts._objectkey_.Index>()?
                        .BindParameters(new ObjectKeyParameter(home.Key));

                if (uri is null)
                {
                    return null;
                }

                var descriptor = DataServiceDescriptor.DraftData(uri.ToString());

                foreach (var domain in DataServiceBuilder.DeriveDomains(typeof(global::KleeneStar.Core.WWW.Api._1_.Drafts._objectkey_.Index)))
                {
                    descriptor.WithDomain(domain);
                }

                return descriptor;
            };
        }

        /// <summary>
        /// Renders the editor for a caller who may edit the home page.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return DocumentHome.MayEdit(ResolveObject(renderContext), renderContext?.Request)
                ? base.Render(renderContext, visualTree)
                : null;
        }

        /// <summary>
        /// Resolves the home page of the workspace the route names.
        /// </summary>
        /// <param name="renderContext">The render context carrying the workspace key.</param>
        /// <returns>The home page, or <see langword="null"/>.</returns>
        protected override Model.Entities.Object ResolveObject(IRenderControlContext renderContext)
        {
            return DocumentHome.Resolve(renderContext?.Request);
        }
    }
}
