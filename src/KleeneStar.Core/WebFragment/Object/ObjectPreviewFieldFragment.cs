using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
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
    /// The field values of the reduced object view: the fields of the view form configured on
    /// the object's class, as an <see cref="ObjectFieldSheet"/>.
    /// </summary>
    /// <remarks>
    /// The sheet is the one <see cref="ObjectItemDetailFragment"/> shows on the object page -
    /// values in the shape their type reads, edited in place by a caller who may change the
    /// object, and the tabs of the view form as a tab strip, built by the same
    /// <see cref="ObjectFieldSheet.BuildForm"/>.
    /// <para>
    /// The two views disagree about what to leave out, and deliberately so. The reading view
    /// drops workflow and date fields because its property column carries them; the pane never
    /// receives that column, so it keeps the date fields and drops only the workflow fields -
    /// the status is already in the headline beside the summary, rendered by
    /// <see cref="ObjectMetadataStatusFragment"/>. The fields the object has not filled are
    /// folded into one closed group after the filled ones: it is the modelled field count of
    /// the class that makes a pane long, and an empty row earns its place only once somebody
    /// wants to fill it in.
    /// </para>
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Preview>]
    [Order(2)]
    [Cache]
    public sealed class ObjectPreviewFieldFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <remarks>
        /// The view form is not read through an injected manager: which form is the class's is
        /// a decision <see cref="ObjectFormLayout.ResolveStandardForm"/> owns for every surface
        /// that presents a form, and a second copy of it here is how the pane and the sheet
        /// would come to answer differently.
        /// </remarks>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the current object
        /// from the URL-bound object key.</param>
        public ObjectPreviewFieldFragment
        (
            IFragmentContext fragmentContext,
            IObjectManager objectManager
        )
            : base(fragmentContext)
        {
            _objectManager = objectManager;
        }

        /// <summary>
        /// Renders the field values. Returns <c>null</c> when the fragment's render conditions
        /// exclude it, when no object can be resolved from the request, or when the view form
        /// places no field the reduced view shows.
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

            // the status sits in the headline beside the summary, the description has its own
            // block; the dates stay, because the pane receives no property column
            var details = ObjectFieldSheet.BuildForm
            (
                "object-preview-field",
                @object,
                x => !ObjectFieldSheet.IsDescriptionAlias(x) && x.FieldType != FieldType.Workflow,
                ContentAuthorization.MayWrite(@object, renderContext?.Request),
                ObjectItemDetailFragment.ResolveObjectRestUri(@object, renderContext),
                collapseEmpty: true
            );

            if (details is null)
            {
                // a class without a view form gets no empty section - the identity block above
                // already names the object
                return null;
            }

            var section = new ControlSection("object-preview-field-section")
            {
                Header = _ => "kleenestar.core:object.detail.card.header",
                HeaderIcon = _ => new IconTableList(),
                Layout = _ => TypeLayoutSection.Rule
            };

            section.Add(details);

            return section.Render(renderContext, visualTree);
        }
    }
}
