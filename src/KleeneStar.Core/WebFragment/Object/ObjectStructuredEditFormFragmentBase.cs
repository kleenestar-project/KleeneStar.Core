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
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The structured input mask of an object: the form the class's
    /// <see cref="FormType.Edit"/> form describes, reproduced one-to-one as
    /// <see cref="IControlFormItem"/> instances over the object CRUD endpoint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the writing half of the form renderer, and the only one there is - the issue
    /// edit dialog and the edit route of a form-rendered document are the same mask, built
    /// from the same form of the same class, and differ in nothing but where they stand.
    /// The subclasses therefore carry attributes and no code: which routes the mask appears
    /// on, in which section, and - on the kinds that offer a choice of renderer - the
    /// condition that decides whether it is this mask or the WYSIWYG editor.
    /// </para>
    /// <para>
    /// <see cref="Summary"/> is emitted before the configured structure because every object
    /// carries a summary whatever its class models, and the classification is reported
    /// through <see cref="ObjectFormLayout.CreateSecurityLevelNotice"/> but never edited
    /// here - who may see the record is not part of the record's content.
    /// </para>
    /// </remarks>
    public abstract class ObjectStructuredEditFormFragmentBase : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the input text control for specifying the summary of the object. This
        /// system field is always rendered first because every object carries a summary,
        /// regardless of the form configuration.
        /// </summary>
        public ControlDataFormItemInputUnique Summary { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Object.Summary),
            Label = _ => "kleenestar.core:object.summary.label",
            Placeholder = _ => "kleenestar.core:object.summary.placeholder",
            Help = _ => "kleenestar.core:object.summary.help",
            Required = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces.UniqueName>().ToString())};

        /// <summary>
        /// Gets the input text control for specifying the description of the object. This
        /// system field is rendered after the summary when no edit form structure is
        /// configured on the class.
        /// </summary>
        public ControlFormItemInputText Description { get; } = new ControlFormItemInputText()
        {
            Name = _ => nameof(Model.Entities.Object.Description),
            Label = _ => "kleenestar.core:object.description.label",
            Placeholder = _ => "kleenestar.core:object.description.placeholder",
            Format = _ => TypeEditTextFormat.Wysiwyg,
            Required = _ => false
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        protected ObjectStructuredEditFormFragmentBase(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // The form's REST service is declared by the endpoint type so the
            // client loads and submits the object through the emitted
            // wx-service island. ItemId addresses the row in the body.
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Objects.Index>();

            ItemId = renderContext =>
            {
                var objectKey = renderContext.Request.GetParameter<ObjectKeyParameter>();
                var @object = CoreHub.ObjectManager.GetObjectByKey(objectKey);
                return @object?.Id.ToString();
            };
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
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var keyParam = renderContext.Request.GetParameter<ObjectKeyParameter>();
            var @object = CoreHub.ObjectManager.GetObjectByKey(keyParam);
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request);
            var items = BuildItems(@object, identityId);

            return base.Render(renderContext, visualTree, items);
        }

        /// <summary>
        /// Builds the form items from the configured edit form. The system field
        /// <see cref="Summary"/> is always emitted first; the rest of the structure is
        /// reproduced from the form's tabs, groups, and field references by the shared
        /// layout builder, which the creation wizard renders its last step from as well.
        /// When no active edit form exists, only the system fields are rendered.
        /// </summary>
        /// <param name="object">The object the form is built for.</param>
        /// <param name="identityId">The identity the form is rendered for.</param>
        /// <returns>The form items.</returns>
        private IEnumerable<IControlFormItem> BuildItems(Model.Entities.Object @object, Guid identityId)
        {
            yield return Summary;

            // the classification is reported here but never edited here: it decides who sees
            // the record, which is not part of the record's content. It is changed in the
            // dialog behind the 'security level' entry of the overflow menu - the surface that
            // replaced the object's permission dialog
            var notice = @object is not null
                ? ObjectFormLayout.CreateSecurityLevelNotice(@object.ClassId, identityId, @object.SecurityLevelId)
                : null;

            if (notice is not null)
            {
                yield return notice;
            }

            var form = @object is not null
                ? ObjectFormLayout.ResolveStandardForm(@object.ClassId, FormType.Edit)
                : null;

            var structure = @object is not null
                ? ObjectFormLayout.BuildItems(form, @object.ClassId).ToList()
                : [];

            if (structure.Count == 0)
            {
                yield return Description;
                yield break;
            }

            foreach (var item in structure)
            {
                yield return item;
            }
        }
    }
}
