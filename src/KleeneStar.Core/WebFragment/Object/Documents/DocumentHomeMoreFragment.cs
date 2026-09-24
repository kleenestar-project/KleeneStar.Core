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
    /// Dropdown item in the more menu of a workspace's document overview that opens the
    /// home-page picker: which of the workspace's documents the overview opens on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It sits on the <b>overview</b> rather than on a document, because that is where the
    /// question is asked. Standing on one page and being offered "make this the home page" means
    /// changing the whole workspace from inside one of its documents, and the page one is
    /// currently reading is the last place from which the alternatives are visible. Here the
    /// picker lists every document of the workspace and shows which one is chosen.
    /// </para>
    /// <para>
    /// It opens as a modal at <see cref="global::KleeneStar.Core.WWW.Documents._workspacekey_.Home"/>,
    /// whose form is <see cref="DocumentHomeFormFragment"/>.
    /// </para>
    /// </remarks>
    [Section<SectionHeadlineMorePrimary>]
    [Scope<global::KleeneStar.Core.WWW.Documents._workspacekey_.Index>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<global::KleeneStar.Core.WebPolicies.WorkspaceAdminPolicy>>]
    [Cache]
    public sealed class DocumentHomeMoreFragment : FragmentControlDropdownItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services
        /// for its operation. Cannot be null.
        /// </param>
        public DocumentHomeMoreFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.core:workspace.home.label";
            Icon = _ => new IconHouse();
            PrimaryAction = renderContext => new ActionModal
            (
                "modal-form",
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Documents._workspacekey_.Home>()?
                    .BindParameters(renderContext.Request),
                TypeModalSize.Large
            );
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">
        /// The context in which the control is rendered.
        /// </param>
        /// <param name="visualTree">
        /// The visual tree representing the control's structure.
        /// </param>
        /// <returns>
        /// An HTML node representing the rendered control.
        /// </returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
