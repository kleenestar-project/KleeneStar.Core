using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using System.Text.Json;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// <i>Move to another workspace…</i> in the overflow menu of an object page: opens the transfer
    /// dialog (<c>Assets/js/transfer.js</c>), which shows the mapping of classes, fields, workflow
    /// states and security levels before anything changes (<c>kleenestar.hierarchy.md</c>,
    /// <i>Move Across Workspaces</i>).
    /// </summary>
    /// <remarks>
    /// The entry links to the page itself with the fragment <c>#ks-transfer</c>; what the dialog
    /// needs - the operations endpoint of the object's workspace, the object's key and name - is
    /// written into <c>&lt;meta name="kleenestar.transfer"&gt;</c>. Offered to a signed-in caller who
    /// may change the object; the endpoint judges every object of the subtree again.
    /// </remarks>
    [Section<SectionHeadlineMorePreferences>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Cache]
    public sealed class ObjectTransferMoreFragment : FragmentControlDropdownItemLink
    {
        /// <summary>
        /// The name of the meta element the dialog reads.
        /// </summary>
        public const string MetaName = "kleenestar.transfer";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectTransferMoreFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.core:object.transfer.label";
            Icon = _ => new IconShare();
            Uri = renderContext => renderContext?.Request?.Uri?.SetFragment("ks-transfer");
        }

        /// <summary>
        /// Renders the entry and the meta element when the caller may move the object.
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

            var workspace = CoreHub.WorkspaceManager.GetWorkspace(@object.WorkspaceId);
            var endpoint = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Operations._workspacekey_.Index>()?
                .BindParameters(new WorkspaceKeyParameter(workspace?.Key))?
                .ToString();

            visualTree?.AddMeta(MetaName, JsonSerializer.Serialize(new
            {
                endpoint,
                @object = @object.Key,
                label = string.IsNullOrWhiteSpace(@object.Summary) ? @object.Key : @object.Summary.Trim()
            }));

            return base.Render(renderContext, visualTree);
        }
    }
}
