using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Class
{
    /// <summary>
    /// Class-scoped property card that groups the structural configuration of the class
    /// (access modifier, abstract / sealed flags, inheritance chain) inside a single
    /// <see cref="ControlCard"/>.
    /// </summary>
    [Section<SectionPropertyPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Class._classid_.Index>]
    [Order(11)]
    [Cache]
    public sealed class ClassPropertyConfigurationCardFragment : FragmentControlPanel
    {
        private readonly IClassManager _classManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ClassPropertyConfigurationCardFragment(IFragmentContext fragmentContext, IClassManager classManager)
            : base(fragmentContext)
        {
            _classManager = classManager;
        }

        /// <summary>
        /// Renders the configuration card. Returns <c>null</c> when no class can be
        /// resolved from the request.
        /// </summary>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var classId = renderContext?.Request?.GetParameter<ClassIdParameter>();
            var @class = _classManager.GetClass(classId);

            if (@class is null)
            {
                return null;
            }

            var card = new ControlCard("class-property-configuration-card")
            {
                Header = _ => "kleenestar.core:class.property.configuration.header",
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Two)
            };

            card.Add(new ControlAttribute("class-property-accessmodifier")
            {
                Icon = _ => new IconLock(),
                Key = _ => "kleenestar.core:class.accessmodifier.label",
                Value = ctx => I18N.Translate(ctx, @class.AccessModifier.Text())
            });

            card.Add(new ControlAttribute("class-property-abstract")
            {
                Icon = _ => new IconShapes(),
                Key = _ => "kleenestar.core:class.isabstract.label",
                Value = ctx => I18N.Translate(ctx, @class.IsAbstract
                    ? "kleenestar.core:class.property.yes"
                    : "kleenestar.core:class.property.no")
            });

            card.Add(new ControlAttribute("class-property-sealed")
            {
                Icon = _ => new IconLock(),
                Key = _ => "kleenestar.core:class.sealed.label",
                Value = ctx => I18N.Translate(ctx, @class.Sealed
                    ? "kleenestar.core:class.property.yes"
                    : "kleenestar.core:class.property.no")
            });

            card.Add(new ControlAttribute("class-property-inherited")
            {
                Icon = _ => new IconCodeBranch(),
                Key = _ => "kleenestar.core:class.inherited.label",
                Value = ctx => ResolveInheritedName(ctx, @class)
            });

            return card.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Resolves the display name of the class this class inherits from. Returns the
        /// localized "none" placeholder when no inheritance is configured.
        /// </summary>
        private string ResolveInheritedName(IRenderControlContext renderContext, Model.Entities.Class @class)
        {
            if (@class.InheritedId is null || @class.InheritedId.Value == Guid.Empty)
            {
                return I18N.Translate(renderContext, "kleenestar.core:class.property.none");
            }

            var inherited = _classManager.GetClass(@class.InheritedId.Value);
            return inherited?.Name ?? I18N.Translate(renderContext, "kleenestar.core:class.property.none");
        }
    }
}
