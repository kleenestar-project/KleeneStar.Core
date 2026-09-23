using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPolicies;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Dropdown item in the object headline 'more' overflow menu that opens the version history
    /// of the current object, alongside "Move", "Export" and "Permissions".
    /// </summary>
    /// <remarks>
    /// The entry targets the body-level page modal rendered by
    /// <see cref="ObjectHistoryModalFragment"/>. A form modal would inject only the children of a
    /// <c>&lt;form&gt;</c> and leave the master-detail dialog empty.
    /// <para>
    /// Visibility follows the object view policy, which carries the <c>object_read_history</c>
    /// permission: a user who may not read the history is not offered a menu entry that would
    /// only answer with an empty dialog.
    /// </para>
    /// </remarks>
    [Section<SectionHeadlineMorePrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<ObjectViewPolicy>>]
    [Cache]
    public sealed class ObjectItemHistoryMoreFragment : FragmentControlDropdownItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for
        /// its operation. Cannot be null.
        /// </param>
        public ObjectItemHistoryMoreFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.core:object.history.label";
            Icon = _ => new IconClockRotateLeft();
            PrimaryAction = renderContext => new ActionModal
            (
                ObjectHistoryModalFragment.ModalId,
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.History>()
                    .BindParameters(renderContext.Request),
                TypeModalSize.ExtraLarge
            );
        }

        /// <summary>
        /// Convert the fragment to HTML. Returns <c>null</c> when the request addresses no
        /// object, so the menu of a page that lost its key carries no entry that could only fail.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>
        /// An HTML node representing the rendered fragment, or <c>null</c>.
        /// </returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var objectKey = renderContext?.Request?.GetParameter<ObjectKeyParameter>();

            if (CoreHub.ObjectManager.GetObjectByKey(objectKey) is null)
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
