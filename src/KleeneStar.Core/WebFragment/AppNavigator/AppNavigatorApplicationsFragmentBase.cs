using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.AppNavigator
{
    /// <summary>
    /// Lists all installed applications as entries of the app navigator, so the user can switch
    /// between the applications hosted by this server.
    /// </summary>
    /// <remarks>
    /// A fragment normally contributes a single dropdown entry. Because the number of installed
    /// applications is only known at runtime, this fragment renders one entry per application and
    /// returns them as an <see cref="HtmlList"/>, which emits its children without a wrapping tag and
    /// therefore keeps the entries as direct siblings of the surrounding dropdown list.
    /// <para>
    /// The class carries no section or scope attributes on purpose: each application registers its own
    /// derived fragment, because a fragment only contributes to the application whose plugin declares it.
    /// </para>
    /// </remarks>
    public abstract class AppNavigatorApplicationsFragmentBase : FragmentControlDropdownItemLink
    {
        private readonly IComponentHub _componentHub;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub used to resolve the installed applications.</param>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        protected AppNavigatorApplicationsFragmentBase(IComponentHub componentHub, IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            _componentHub = componentHub;
        }

        /// <summary>
        /// Convert the control to HTML.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>
        /// An HTML node containing one entry per installed application, or null when the fragment is
        /// not applicable for the current request.
        /// </returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var applications = _componentHub?.ApplicationManager?.Applications ?? [];
            var html = new HtmlList();

            foreach (var application in applications
                .OrderBy(x => I18N.Translate(renderContext, x?.ApplicationName)))
            {
                var item = new ControlDropdownItemLink($"{Id}-{application?.ApplicationId}")
                {
                    Text = _ => application?.ApplicationName,
                    // unlike Text, the tooltip is emitted verbatim by the control, so the
                    // application description has to be resolved here to avoid leaking the raw key
                    Tooltip = _ => I18N.Translate(renderContext, application?.Description),
                    Uri = _ => application?.Route?.ToUri(),
                    Icon = _ => new ImageIcon
                    (
                        application?.Icon?.ToUri(),
                        new PropertySizeIcon(1, TypeSizeUnit.Em)
                    )
                };

                var node = item.Render(renderContext, visualTree);

                if (node != null)
                {
                    html.Add(node);
                }
            }

            return html;
        }
    }
}
