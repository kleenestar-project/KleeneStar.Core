using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
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
    /// Object-scoped property card that renders the tags (labels) attached to the current
    /// object on <see cref="WWW.Issue._objectkey_.Index"/> as a row of colored badges.
    /// </summary>
    /// <remarks>
    /// The tags are resolved through <see cref="IObjectTagManager"/>. Each tag is rendered as
    /// a <see cref="ControlBadge"/> whose background is the tag's stored
    /// <see cref="ObjectTag.Color"/>, or a color derived deterministically from the tag name
    /// when no color is stored. The card is hidden when the object carries no tags.
    /// </remarks>
    [Section<SectionPropertyPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Order(7)]
    [Cache]
    public sealed class ObjectPropertyTagCardFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;
        private readonly IObjectTagManager _tagManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the current object
        /// from the URL-bound object key.</param>
        /// <param name="tagManager">The tag manager used to load the object's tags.</param>
        public ObjectPropertyTagCardFragment(IFragmentContext fragmentContext, IObjectManager objectManager, IObjectTagManager tagManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _tagManager = tagManager;
        }

        /// <summary>
        /// Renders the tag card for the current object. Returns <c>null</c> when the
        /// fragment's render conditions exclude it, when no object can be resolved from the
        /// request, or when the object carries no tags.
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

            var tags = _tagManager.GetTags(@object.Id).ToList();

            if (tags.Count == 0)
            {
                return null;
            }

            var section = new ControlSection("object-property-tag-section")
            {
                Header = _ => "kleenestar.core:object.property.tag.header",
                HeaderIcon = _ => new IconTags(),
                Layout = _ => TypeLayoutSection.Rule
            };

            var list = new ControlPanel("object-tag-list")
            {
                Styles = ["display: flex; flex-wrap: wrap; gap: 0.35em;"]
            };

            foreach (var tag in tags)
            {
                list.Add(ObjectTagBadge.Create(tag, "object-tag-"));
            }

            section.Add(list);

            return section.Render(renderContext, visualTree);
        }
    }
}
