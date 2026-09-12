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
    /// <see cref="Summary"/> is not one of the items: every object carries a summary whatever
    /// its class models, and it is the <em>name</em> of what is being edited rather than one
    /// of its answers, so it is rendered into the form's <c>header</c>. Opened as a dialog -
    /// which is how an issue and an asset are edited - the framework lifts that header onto
    /// the dialog's title bar, so the record is titled by its own summary instead of by a
    /// generic caption, exactly as the prose editor titles a document.
    /// </para>
    /// <para>
    /// The classification is neither edited nor mentioned here. Who may see the record is
    /// not part of the record's content, and it is set in its own dialog behind the
    /// <em>security level</em> entry of the overflow menu; the notice
    /// <see cref="ObjectFormLayout.CreateSecurityLevelNotice"/> writes for the creation
    /// wizard and the clone form has nothing to say on an edit, because an edit never
    /// changes the level - the record the caller opened stays where it is.
    /// </para>
    /// </remarks>
    public abstract class ObjectStructuredEditFormFragmentBase : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the input control for the summary of the object - the name the record is
        /// titled by. It is rendered into the form's header instead of among its items, and
        /// therefore becomes the title of the dialog the mask is opened as.
        /// </summary>
        /// <remarks>
        /// It carries no label and no help line: a caption reading <em>summary</em> over the
        /// name of the thing on screen explains nothing, and a title bar is no place for a
        /// sentence about the field. The placeholder says what belongs there while the field
        /// is empty, which is the only moment the question arises.
        /// <para>
        /// It is a plain text input rather than the <c>ControlDataFormItemInputUnique</c> it
        /// used to be. That control checked the summary against the <em>workspace</em> names
        /// (<c>/api/1/workspaces/uniquename</c>) and refused the reserved workspace keys, so
        /// an issue called like a workspace was reported as taken although two objects may
        /// carry the same summary and nothing ever refused one. The creation wizard has always
        /// used the plain input (<see cref="ObjectFormLayout.CreateSummaryInput"/>); the two
        /// paths now agree, and the availability badge that check painted has no place on a
        /// title bar anyway.
        /// </para>
        /// </remarks>
        public ControlFormItemInputText Summary { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Object.Summary),
            Placeholder = _ => "kleenestar.core:object.summary.placeholder",
            Required = _ => true,

            // the framework's mark for an input that is the dialog's title: no frame, the
            // title bar's own font, the whole width of it, the affordances on hover and focus
            Classes = [FormTitleInput.Mark]
        };

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
            var items = BuildItems(@object);

            // the summary titles the form: it goes onto the header, which the dialog lifts
            // onto its title bar, rather than among the answers of the mask
            return FormTitleInput.Place(base.Render(renderContext, visualTree, items), Summary, renderContext, visualTree);
        }

        /// <summary>
        /// Builds the form items from the configured edit form: the structure of the class's
        /// edit form, reproduced from its tabs, groups, and field references by the shared
        /// layout builder, which the creation wizard renders its last step from as well. When
        /// no active edit form exists, only the system description is rendered.
        /// </summary>
        /// <remarks>
        /// <see cref="Summary"/> is deliberately absent: it titles the record rather than
        /// answering one of its questions, and is rendered onto the form's header by
        /// <see cref="FormTitleInput"/>.
        /// </remarks>
        /// <param name="object">The object the form is built for.</param>
        /// <returns>The form items.</returns>
        private IEnumerable<IControlFormItem> BuildItems(Model.Entities.Object @object)
        {
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
