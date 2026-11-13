using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

using ObjectEntity = KleeneStar.Model.Entities.Object;
using SecurityLevelEntity = KleeneStar.Model.Entities.SecurityLevel;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The classification dialog of a single object: one tile selection
    /// (<see cref="ControlFormItemInputTile"/>) naming the security level the object carries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dialog edits the object and submits <see cref="ObjectEntity.SecurityLevelId"/> to the
    /// object CRUD endpoint (<c>/api/1/objects</c>) with the object resolved from the route. The
    /// classification is not a field of the edit form.
    /// </para>
    /// <para>
    /// Tiles: <c>securitylevel.none.label</c> with the empty guid (the form binder clears the
    /// property), then the active levels of the class the caller may assign
    /// (<see cref="WebManager.ISecurityLevelManager.GetAssignableSecurityLevels"/>), ordered by
    /// <see cref="SecurityLevelEntity.Rank"/> and name. A level the object carries and the caller
    /// cannot assign is added as a tile of its own, so the posted value keeps it; the endpoint
    /// accepts an unchanged classification. Each level tile shows icon, name, description, rank,
    /// the default marker and the cleared groups (<see cref="SecurityLevelEntity.PermittedGroupIds"/>).
    /// The tile of the current classification carries the chip <c>securitylevel.object.current</c>
    /// and is preselected (<see cref="PresetInputTile"/>). The search field narrows the tiles by
    /// their text.
    /// </para>
    /// <para>
    /// Without an assignable level the selection is omitted and
    /// <see cref="ObjectFormLayout.CreateSecurityLevelNotice"/> states the reason.
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
        /// The items are built per render, because the tiles, the preselection and the notice
        /// depend on the object of the route and on the caller's clearance.
        /// </remarks>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var objectKey = renderContext.Request.GetParameter<ObjectKeyParameter>();
            var @object = CoreHub.ObjectManager.GetObjectByKey(objectKey);
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request);

            return base.Render(renderContext, visualTree, BuildItems(renderContext, @object, identityId));
        }

        /// <summary>
        /// Builds the items of the dialog: the notice, then the tile selection.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="object">The object being classified, or <c>null</c>.</param>
        /// <param name="identityId">The identity the dialog is rendered for.</param>
        /// <returns>The form items.</returns>
        private static IEnumerable<IControlFormItem> BuildItems(IRenderControlContext renderContext, ObjectEntity @object, Guid identityId)
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

            var selection = SecurityLevelTiles.Create(renderContext, @object, identityId);

            if (selection is not null)
            {
                yield return selection;
            }
        }
    }
}
