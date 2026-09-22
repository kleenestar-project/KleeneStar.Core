using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using System;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The classification section of the reference zone: the security level the object
    /// carries and the groups cleared for it.
    /// </summary>
    /// <remarks>
    /// The section is absent on an unclassified object rather than reporting "none". An
    /// unclassified object is the normal case, and a section that is always there stops being
    /// a signal - the same reasoning the lifecycle card applies to its archived badge.
    /// <para>
    /// Anybody who can see this page is by definition cleared for the level, so naming it here
    /// discloses nothing they do not already have. What it does disclose is <i>who else</i>
    /// sees the record, which is the question somebody about to write on it needs answered.
    /// </para>
    /// </remarks>
    [Section<SectionPropertyPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Order(10)]
    [Cache]
    public sealed class ObjectPropertySecurityLevelCardFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        /// <param name="objectManager">The object manager the object is resolved through.</param>
        public ObjectPropertySecurityLevelCardFragment(IFragmentContext fragmentContext, IObjectManager objectManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
        }

        /// <summary>
        /// Renders the classification card. Returns <c>null</c> when no object can be resolved
        /// from the request or the object carries no classification.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var keyParameter = renderContext?.Request?.GetParameter<ObjectKeyParameter>();
            var @object = _objectManager.GetObjectByKey(keyParameter?.Value);
            var securityLevel = @object?.SecurityLevelId is null
                ? null
                : CoreHub.SecurityLevelManager.GetSecurityLevel(@object.SecurityLevelId.Value);

            if (securityLevel is null)
            {
                return null;
            }

            var section = new ControlSection("object-property-securitylevel-section")
            {
                Header = _ => "kleenestar.core:securitylevel.object.label",
                HeaderIcon = _ => new IconShieldHalved(),
                Layout = _ => TypeLayoutSection.Rule,
                Badge = ctx => securityLevel.Name,
                BadgeColor = _ => new PropertyColorBackgroundBadge(TypeColorBackgroundBadge.Warning)
            };

            if (!ProseText.IsEmpty(securityLevel.Description))
            {
                section.Add(new ControlAttribute("object-property-securitylevel-description")
                {
                    Icon = _ => new IconInfo(),
                    Key = _ => "kleenestar.core:securitylevel.description.label",
                    Value = _ => ProseText.ToPlainText(securityLevel.Description)
                });
            }

            section.Add(new ControlAttribute("object-property-securitylevel-clearance")
            {
                Icon = _ => new IconUsers(),
                Key = _ => "kleenestar.core:securitylevel.clearance.label",
                Value = ctx => Clearance(ctx, securityLevel)
            });

            return section.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Names the groups the level clears.
        /// </summary>
        /// <param name="renderContext">The render context, for the culture of the message.</param>
        /// <param name="securityLevel">The level the object carries. Never null.</param>
        /// <returns>The group names, or the word standing for "nobody".</returns>
        private static string Clearance(IRenderControlContext renderContext, Model.Entities.SecurityLevel securityLevel)
        {
            var groups = CoreHub.GroupManager
                .GetGroups(new Query<Model.Entities.Group>())
                .ToDictionary(x => x.Id, x => x.Name);

            var names = (securityLevel.PermittedGroupIds ?? [])
                .Select(x => groups.TryGetValue(x, out var name) ? name : null)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            return names.Count == 0
                ? I18N.Translate(renderContext, "kleenestar.core:securitylevel.clearance.none")
                : string.Join(", ", names);
        }
    }
}
