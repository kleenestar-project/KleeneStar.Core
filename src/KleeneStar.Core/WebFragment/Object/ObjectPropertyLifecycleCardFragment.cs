using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System.Globalization;
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

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The lifecycle section of the reference zone, grouping the creation timestamp, the last
    /// update timestamp and the lifecycle state of the object.
    /// </summary>
    /// <remarks>
    /// An archived object is reported in the section header as well, where a folded section
    /// still shows it: the archived state changes how everything else on the page should be
    /// read. An active object is the normal case and gets no badge - a badge that is always
    /// there stops being a signal.
    /// </remarks>
    [Section<SectionPropertyPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Order(9)]
    [Cache]
    public sealed class ObjectPropertyLifecycleCardFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ObjectPropertyLifecycleCardFragment(IFragmentContext fragmentContext, IObjectManager objectManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
        }

        /// <summary>
        /// Renders the lifecycle card. Returns <c>null</c> when no object can be
        /// resolved from the request.
        /// </summary>
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

            // an archived object is the one lifecycle state that changes how everything else on
            // the page should be read, so it is reported in the header where a folded section
            // still shows it. an active object is the normal case and gets no badge - a badge
            // that is always there stops being a signal.
            var archived = @object.State == WorkspaceState.Archived;

            var section = new ControlSection("object-property-lifecycle-section")
            {
                Header = _ => "kleenestar.core:object.property.lifecycle.header",
                HeaderIcon = _ => new IconClockRotateLeft(),
                Layout = _ => TypeLayoutSection.Rule,
                Badge = archived ? ctx => I18N.Translate(ctx, @object.State.Text()) : null,
                BadgeColor = archived ? _ => new PropertyColorBackgroundBadge(TypeColorBackgroundBadge.Secondary) : null
            };

            section.Add(new ControlAttribute("object-property-created")
            {
                Icon = _ => new IconCalendarPlus(),
                Key = _ => "kleenestar.core:object.created.label",
                // the card is read in the visitor's language, so the timestamp is written in
                // the visitor's culture as well - an invariant 07/25/2026 on a German page
                // reads as a different date than the one that is meant
                Value = ctx => @object.Created.ToString("g", Culture(ctx))
            });

            section.Add(new ControlAttribute("object-property-updated")
            {
                Icon = _ => new IconClockRotateLeft(),
                Key = _ => "kleenestar.core:object.updated.label",
                Value = ctx => @object.Updated.ToString("g", Culture(ctx))
            });

            section.Add(new ControlFlex
            (
                "object-property-state",
                new ControlIcon
                {
                    Icon = _ => new IconTrafficLight(),
                    Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.One, PropertySpacing.Space.None, PropertySpacing.Space.None)
                },
                new ControlText
                {
                    Text = ctx => I18N.Translate(ctx, "kleenestar.core:object.state.label") + ":",
                    Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.One, PropertySpacing.Space.None, PropertySpacing.Space.None)
                },
                new ControlBadge("object-property-state-badge")
                {
                    Value = ctx => I18N.Translate(ctx, @object.State.Text()),
                    Pill = _ => TypePillBadge.Pill,
                    BackgroundColor = _ => new PropertyColorBackgroundBadge(MapStateBadgeColor(@object.State))
                }
            )
            {
                Layout = _ => TypeLayoutFlex.Default,
                Align = _ => TypeAlignFlex.Center,
                Justify = _ => TypeJustifiedFlex.Start
            });

            return section.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Returns the culture the request is rendered in, falling back to the invariant
        /// culture when the request carries none.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The culture.</returns>
        private static CultureInfo Culture(IRenderControlContext renderContext)
        {
            return renderContext?.Request?.Culture ?? CultureInfo.InvariantCulture;
        }

        /// <summary>
        /// Maps a workspace lifecycle state to the badge background color used to render it
        /// in the lifecycle card: green for active, grey for archived.
        /// </summary>
        /// <param name="state">The lifecycle state.</param>
        /// <returns>The badge background color.</returns>
        private static TypeColorBackgroundBadge MapStateBadgeColor(WorkspaceState state)
        {
            return state switch
            {
                WorkspaceState.Active => TypeColorBackgroundBadge.Success,
                WorkspaceState.Archived => TypeColorBackgroundBadge.Secondary,
                _ => TypeColorBackgroundBadge.Default
            };
        }
    }
}
