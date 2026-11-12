using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object.Documents
{
    /// <summary>
    /// Button in the headline of a workspace's document overview that opens the home document
    /// itself - the page the overview is showing a preview of.
    /// </summary>
    /// <remarks>
    /// It sits beside the more menu rather than inside the preview card, because opening the
    /// page is the one thing a reader of that card wants next, and an action buried in the body
    /// of a card is found late. The card keeps the title and the text; going there is a button.
    /// <para>
    /// It renders nothing when the workspace holds no documents, which is the same condition
    /// under which the card says so instead of previewing one - there is nothing to open.
    /// </para>
    /// </remarks>
    [Section<SectionHeadlinePrimary>]
    [Scope<global::KleeneStar.Core.WWW.Documents._workspacekey_.Index>]
    [Cache]
    public sealed class DocumentHomeOpenButtonFragment : FragmentControlButtonLink
    {
        private readonly IWorkspaceManager _workspaceManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for
        /// its operation. Cannot be null.
        /// </param>
        /// <param name="workspaceManager">
        /// The workspace manager naming the home document. Cannot be null.
        /// </param>
        public DocumentHomeOpenButtonFragment(IFragmentContext fragmentContext, IWorkspaceManager workspaceManager)
            : base(fragmentContext)
        {
            _workspaceManager = workspaceManager;

            Text = _ => "kleenestar.core:object.kind.documents.open.label";
            Icon = _ => new IconFileLines();
            Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two);
            BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Primary);
            Uri = renderContext => CoreHub.ObjectKindManager.ResolveDetailUri(Resolve(renderContext));
        }

        /// <summary>
        /// Renders the control as an HTML node, or nothing when the workspace has no document to
        /// open.
        /// </summary>
        /// <param name="renderContext">
        /// The context in which the control is rendered.
        /// </param>
        /// <param name="visualTree">
        /// The visual tree representing the control's structure.
        /// </param>
        /// <returns>
        /// An HTML node representing the rendered control, or <c>null</c>.
        /// </returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request) || Resolve(renderContext) is null)
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Resolves the home document of the workspace addressed by the request.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The home document, or <see langword="null"/>.</returns>
        private Model.Entities.Object Resolve(IRenderControlContext renderContext)
        {
            var keyParameter = renderContext?.Request?.GetParameter<WorkspaceKeyParameter>();
            var workspace = _workspaceManager.GetWorkspaceByKey(keyParameter?.Value);

            return workspace is null ? null : _workspaceManager.GetHome(workspace.Id);
        }
    }
}
