using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using System.Runtime.CompilerServices;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Workspace
{
    /// <summary>
    /// The base of an entry of the workspace cog (<see cref="WorkspaceSidebarSettingFragment"/>):
    /// a dropdown link to a page of the workspace the menu stands for - a page of its own, or a
    /// dialog when <see cref="ModalSize"/> is set.
    /// </summary>
    /// <remarks>
    /// An entry is a fragment of <see cref="SectionWorkspaceSettingPrimary"/> (or
    /// <see cref="SectionWorkspaceSettingSecondary"/>) with an <c>[Order]</c> - the core's at
    /// 10…100 - and <c>[Scope&lt;IScopeGeneral&gt;]</c>: the container decides on which pages the
    /// menu stands and who sees it, an entry only where it leads. It must be <b>public sealed</b>,
    /// or WebExpress never finds it. An entry whose page has no address is hidden
    /// (<see cref="ControlDropdownItemLink.Visible"/>), because a link without one lands on the
    /// start page.
    /// </remarks>
    public abstract class WorkspaceSettingItemFragment : FragmentControlDropdownItemLink
    {
        private static readonly ConditionalWeakTable<IRequest, string> _workspaceKeys = new();

        /// <summary>
        /// Gets the size of the dialog the entry opens, or <see langword="null"/> for a page.
        /// </summary>
        protected virtual TypeModalSize? ModalSize => null;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        protected WorkspaceSettingItemFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Visible = renderContext => Destination(renderContext) is not null;
            Uri = renderContext => ModalSize is null ? Destination(renderContext) : null;
            PrimaryAction = renderContext => ModalSize is { } size && Destination(renderContext) is { } uri
                ? new ActionModal("modal-form", uri, size)
                : null;
        }

        /// <summary>
        /// Returns the address the entry leads to, bound to the menu's workspace.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The address, or <see langword="null"/> to hide the entry.</returns>
        protected abstract IUri Destination(IRenderControlContext renderContext);

        /// <summary>
        /// Returns the address of a page bound to the menu's workspace.
        /// </summary>
        /// <typeparam name="TPage">The page; its route names the workspace key.</typeparam>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The address, or <see langword="null"/> when the page or the workspace is unknown.</returns>
        protected static IUri UriOf<TPage>(IRenderControlContext renderContext)
            where TPage : IEndpoint
        {
            var key = ResolveWorkspaceKey(renderContext?.Request);

            return string.IsNullOrEmpty(key)
                ? null
                : CoreHub.GetUri<TPage>()?.BindParameters(new WorkspaceKeyParameter(key));
        }

        /// <summary>
        /// Returns the key of the workspace the menu stands for: the route's, else the addressed
        /// object's, else the addressed class's. Asked once per request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The key, or <see langword="null"/>.</returns>
        public static string ResolveWorkspaceKey(IRequest request)
        {
            if (request is null)
            {
                return null;
            }

            if (_workspaceKeys.TryGetValue(request, out var cached))
            {
                return cached;
            }

            var key = request.GetParameter<WorkspaceKeyParameter>()?.Value;

            // on the per-kind detail pages the route carries only the object key
            if (string.IsNullOrEmpty(key))
            {
                var objectKey = request.GetParameter<ObjectKeyParameter>()?.Value;

                key = string.IsNullOrEmpty(objectKey) ? null : CoreHub.ObjectManager.GetObjectByKey(objectKey)?.Workspace?.Key;
            }

            // on the class administration pages the route names a class, or a record of its structure
            if (string.IsNullOrEmpty(key) && RouteAuthorization.ResolveClass(request) is { } @class)
            {
                key = CoreHub.WorkspaceManager.GetWorkspace(@class.WorkspaceId)?.Key;
            }

            _workspaceKeys.AddOrUpdate(request, key);

            return key;
        }
    }
}
