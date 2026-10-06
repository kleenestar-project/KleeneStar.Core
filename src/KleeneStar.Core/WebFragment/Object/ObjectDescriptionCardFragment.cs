using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The narrative section of the object view: the
    /// <see cref="Model.Entities.Object.Description"/> of the current object as an
    /// inline-editable rich-text block on <see cref="WWW.Issue._objectkey_.Index"/>.
    /// </summary>
    /// <remarks>
    /// The section wraps a <see cref="ControlSmartEdit"/> that persists changes through
    /// the object REST API as soon as the user finishes editing. The smart-edit hosts
    /// a multiline <see cref="ControlFormItemInputText"/> in Wysiwyg format so the
    /// description supports the same formatting affordances as the add/edit form
    /// variants used elsewhere. It lives in <see cref="SectionContentPrimary"/> with an
    /// explicit <see cref="OrderAttribute"/> of <c>0</c> so the narrative always opens the
    /// work zone, above the field structure of <see cref="ObjectItemDetailFragment"/>.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Order(0)]
    [Cache]
    public sealed class ObjectDescriptionCardFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the current
        /// object from the URL-bound object key.</param>
        public ObjectDescriptionCardFragment(IFragmentContext fragmentContext, IObjectManager objectManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
        }

        /// <summary>
        /// Renders the description card for the current object. Returns <c>null</c> when
        /// the fragment's render conditions exclude it or when no object can be resolved
        /// from the request.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var keyParameter = renderContext?.Request?.GetParameter<ObjectKeyParameter>();
            var @object = _objectManager.GetObjectByKey(keyParameter?.Value);

            if (@object is null)
            {
                return null;
            }

            var section = new ControlSection("object-description-section")
            {
                Header = _ => "kleenestar.core:object.description.card.header",
                HeaderIcon = _ => new IconAlignLeft(),
                Layout = _ => TypeLayoutSection.Rule
            };

            section.Add(BuildDescriptionSmartEdit(@object, ResolveObjectRestUri(@object, renderContext)));

            return section.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Builds the inline-editable smart-edit bound to the object's
        /// <see cref="Model.Entities.Object.Description"/> system attribute. The
        /// underlying input is a multiline <see cref="ControlFormItemInputText"/> in
        /// Wysiwyg format, and the smart-edit issues a <see cref="RequestMethod.PUT"/>
        /// against the object's REST URI when the value changes.
        /// </summary>
        /// <param name="object">The object whose description is displayed.</param>
        /// <param name="objectUri">The REST URI bound to the object's id; used as the
        /// smart-edit's persistence endpoint.</param>
        /// <returns>The smart-edit control hosting the description input.</returns>
        private static ControlSmartEdit BuildDescriptionSmartEdit(Model.Entities.Object @object, IUri objectUri)
        {
            var name = nameof(Model.Entities.Object.Description);

            var input = new ControlFormItemInputText()
            {
                Name = _ => name,
                Label = _ => "kleenestar.core:object.description.label",
                Placeholder = _ => "kleenestar.core:object.description.placeholder",
                Required = _ => false,
                Format = _ => TypeEditTextFormat.Wysiwyg,
                LinkLibraryUri = CoreHub.EditorLinkLibrary
            };

            var smartEdit = new ControlSmartEdit("attr-description")
            {
                ObjectId = _ => @object.Id.ToString(),
                ObjectName = _ => name,
                Uri = _ => objectUri,
                Method = _ => RequestMethod.PUT
            };

            smartEdit.Add(input);

            smartEdit.Initialize(args => args.SetValue(input, new ControlFormInputValueString(@object.Description)));

            return smartEdit;
        }

        /// <summary>
        /// Returns the REST endpoint that owns the object's persistence, augmented with
        /// the object's id so smart-edit PUTs target the right record.
        /// </summary>
        /// <param name="object">The object whose REST endpoint is resolved.</param>
        /// <param name="renderContext">The current render context; used to bind the URI
        /// to the active request's route parameters.</param>
        /// <returns>The bound REST URI, or <c>null</c> when no endpoint is registered.</returns>
        private static IUri ResolveObjectRestUri(Model.Entities.Object @object, IRenderControlContext renderContext)
        {
            var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Objects.Index>();
            if (uri is null)
            {
                return null;
            }

            var withQuery = uri.Add(new UriQuery("id", @object.Id.ToString()));

            return renderContext?.Request is null
                ? withQuery
                : withQuery.BindParameters(renderContext.Request);
        }
    }
}
