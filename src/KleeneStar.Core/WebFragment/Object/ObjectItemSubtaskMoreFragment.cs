using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Represents a dropdown item in the object headline 'more' overflow menu that opens the
    /// create wizard for a sub-task of the current object.
    /// </summary>
    /// <remarks>
    /// The entry is how an order is decomposed across departments: the wizard lets the caller
    /// file the sub-task in whichever workspace does the work, and the create endpoint joins it
    /// to the order by the aggregating relation, so the order reports the progress of all its
    /// sub-tasks. It is offered to a caller who may relate the current object at all.
    /// </remarks>
    [Section<SectionHeadlineMorePreferences>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Cache]
    public sealed class ObjectItemSubtaskMoreFragment : FragmentControlDropdownItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context associated with the fragment.</param>
        public ObjectItemSubtaskMoreFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.core:object.subtask.add.label";
            Icon = _ => new IconSitemap();
            PrimaryAction = renderContext => new ActionModal
            (
                "modal-form",
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Objects.Add>()?
                    .Add(new UriQuery(ObjectAddFormFragment.SubtaskOfParameter, renderContext?.Request?.GetParameter<ObjectKeyParameter>()?.Value)),
                TypeModalSize.ExtraLarge
            );
        }

        /// <summary>
        /// Convert the fragment to HTML, or to nothing for a caller who may not relate the
        /// current object.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>An HTML node, or <see langword="null"/>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var @object = CoreHub.ObjectManager.GetObjectByKey(renderContext?.Request?.GetParameter<ObjectKeyParameter>()?.Value);

            if (@object is null
                || CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request) == System.Guid.Empty
                || !ContentAuthorization.MayWrite(@object, renderContext.Request, typeof(global::KleeneStar.Core.WebPermissions.ObjectRelationPermission)))
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
