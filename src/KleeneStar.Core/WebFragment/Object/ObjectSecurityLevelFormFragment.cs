using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using System;
using System.Collections.Generic;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The classification dialog of a single object: one selection, naming the security level
    /// the object carries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the surface that replaced the object's permission dialog. Access to a record is
    /// not granted on the record any more - it follows from the classification, and this is
    /// where the classification is set. It is therefore deliberately <b>not</b> a field on the
    /// object's edit form: the edit form is where the content of a record is written, and who
    /// may see the record is not part of its content.
    /// </para>
    /// <para>
    /// It edits the object and submits to the object CRUD endpoint with the object resolved from
    /// the route; a single-property form of an entity that already has a CRUD surface needs no
    /// endpoint of its own. The selection's first entry stands for "unclassified" and carries the
    /// empty guid, which the form binder reads as "clear this property".
    /// </para>
    /// </remarks>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.SecurityLevel>]
    [Cache]
    public sealed class ObjectSecurityLevelFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectSecurityLevelFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Objects.Index>();

            ItemId = renderContext =>
            {
                var objectKey = renderContext.Request.GetParameter<ObjectKeyParameter>();

                return CoreHub.ObjectManager.GetObjectByKey(objectKey)?.Id.ToString();
            };
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <remarks>
        /// The items are built per render rather than declared as properties, because both of
        /// them depend on the class of the object the route names: the selection is fed by the
        /// class-scoped endpoint, and whether the notice appears at all depends on what the
        /// caller is cleared for in that class.
        /// </remarks>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var objectKey = renderContext.Request.GetParameter<ObjectKeyParameter>();
            var @object = CoreHub.ObjectManager.GetObjectByKey(objectKey);
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request);

            return base.Render(renderContext, visualTree, BuildItems(@object, identityId));
        }

        /// <summary>
        /// Builds the items of the dialog: the notice explaining what the caller cannot do, and
        /// the selection where they can.
        /// </summary>
        /// <param name="object">The object being classified, or null when the route names none.</param>
        /// <param name="identityId">The identity the dialog is rendered for.</param>
        /// <returns>The form items.</returns>
        private static IEnumerable<IControlFormItem> BuildItems(ObjectEntity @object, Guid identityId)
        {
            if (@object is null)
            {
                yield break;
            }

            var notice = ObjectFormLayout.CreateSecurityLevelNotice(@object.ClassId, identityId, @object.SecurityLevelId);

            if (notice is not null)
            {
                yield return notice;
            }

            var securityLevel = ObjectFormLayout.CreateSecurityLevelInput(@object.ClassId, identityId);

            if (securityLevel is not null)
            {
                yield return securityLevel;
            }
        }
    }
}
