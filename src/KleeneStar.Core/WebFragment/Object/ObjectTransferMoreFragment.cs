using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// <i>Move to another workspace…</i> in the overflow menu of an object page: opens the transfer
    /// wizard (<see cref="ObjectTransferFormFragment"/> on <see cref="WWW.Issue._objectkey_.Transfer"/>),
    /// which shows the mapping of classes, workflow states, fields and security levels before
    /// anything changes (<c>kleenestar.hierarchy.md</c>, <i>Move Across Workspaces</i>).
    /// </summary>
    /// <remarks>
    /// Offered to a signed-in caller who may change the object; the endpoint judges every object
    /// of the subtree again.
    /// </remarks>
    [Section<SectionHeadlineMorePreferences>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WebScope.IScopeObjectKindDetail>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Preview>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Cache]
    public sealed class ObjectTransferMoreFragment : FragmentControlDropdownItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectTransferMoreFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.core:object.transfer.label";
            Icon = _ => new IconShare();
            PrimaryAction = renderContext => new ActionModal
            (
                "modal-form",
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.Transfer>()
                    .BindParameters(renderContext.Request),
                TypeModalSize.Large
            );
        }

        /// <summary>
        /// Renders the entry when the caller may move the object.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The entry, or <see langword="null"/>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var request = renderContext?.Request;
            var @object = CoreHub.ObjectManager.GetObjectByKey(request?.GetParameter<ObjectKeyParameter>()?.Value);

            if (@object is null
                || CoreHub.SessionManager.GetCurrentIdentityId(request) == System.Guid.Empty
                || !ContentAuthorization.MayWrite(@object, request))
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
