using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The relation surface of the object view: every semantic relation the object holds -
    /// to other objects and to addresses outside the installation - grouped by what the
    /// relation says, with the dialog that establishes a new one.
    /// </summary>
    /// <remarks>
    /// The surface hosts a single <see cref="ControlDataRelationView"/> against three
    /// endpoints: the relations of the object, the link systems the add dialog offers in its
    /// sidebar, and the search for the object a relation points at. Which relations exist is
    /// answered by the server at request time from the administered catalog, so a relation
    /// defined in the class administration appears here without this fragment changing.
    /// <para>
    /// The object is the perspective, not just a filter: a relation stored from the other end
    /// is rendered here under its inverse label, which is why the endpoint is addressed by
    /// object key rather than by a query.
    /// </para>
    /// </remarks>
    [Section<SectionContentSecondary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Relations>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Preview>]
    [Order(1)]
    [Cache]
    public sealed class ObjectRelationCardFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;
        private readonly IObjectRelationManager _relationManager;

        /// <summary>
        /// Gets the REST-backed relation surface.
        /// </summary>
        public ControlDataRelationView Relations { get; } = new("object-relations")
        {
            Subject = renderContext => renderContext?.Request?.GetParameter<ObjectKeyParameter>()?.Value,
            SubjectClass = renderContext => ResolveClassName(renderContext),

            // the section already frames the surface, so the control renders flat rather than
            // as a second card inside the first
            Layout = _ => TypeLayoutRelationView.Flat,

            // ...and the section already carries the icon, the caption and the count, so the
            // control's own header would state all three a second line further down
            HeaderIcon = _ => false,
            HeaderText = _ => false,
            HeaderBadge = _ => false
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the current object
        /// from the URL-bound object key.</param>
        /// <param name="relationManager">The relation manager, read for the count the header
        /// reports.</param>
        public ObjectRelationCardFragment(IFragmentContext fragmentContext, IObjectManager objectManager, IObjectRelationManager relationManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _relationManager = relationManager;

            Relations
                .DataService<global::KleeneStar.Core.WWW.Api._1_.Relations._objectkey_.Index>()
                .SystemsService<global::KleeneStar.Core.WWW.Api._1_.Relations.Systems>()
                .TargetsService<global::KleeneStar.Core.WWW.Api._1_.Relations.Targets>();
        }

        /// <summary>
        /// Renders the relation section for the current object. Returns <c>null</c> when the
        /// fragment's render conditions exclude it or when no object can be resolved.
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

            // the relations themselves arrive from the rest endpoint, but the count is cheap
            // to read here - and it is what makes a folded section still say whether there is
            // anything in it
            var count = _relationManager.GetRelations(@object.Id).Count();

            var section = new ControlSection("object-relations-section")
            {
                Header = _ => "kleenestar.core:object.relations.card.header",
                HeaderIcon = _ => new IconLink(),
                Layout = _ => TypeLayoutSection.Rule,
                Badge = count > 0 ? _ => count.ToString(CultureInfo.InvariantCulture) : null
            };

            section.Add(Relations);

            return section.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Resolves the class name of the object the route addresses, which decides which
        /// relations the add dialog may offer.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The class name, or <c>null</c>.</returns>
        private static string ResolveClassName(IRenderControlContext renderContext)
        {
            var key = renderContext?.Request?.GetParameter<ObjectKeyParameter>()?.Value;

            return ObjectRelationProjection.ClassNameOf(CoreHub.ObjectManager.GetObjectByKey(key));
        }
    }
}
