using KleeneStar.Core.WebFragment.Object;
using System;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// Represents a per-kind object dropdown for the application header. The dropdown is
    /// labelled and iconed by its <see cref="IObjectKind"/> descriptor and its dynamic
    /// items are the calling identity's most recently opened objects of that kind
    /// (supplied by the kind's dropdown REST endpoint). It replaces the former single
    /// object dropdown, so every kind (documents, blogs, issues, assets) owns its own
    /// header dropdown.
    /// </summary>
    public class ObjectKindDropdownControl : ControlDataDropdown
    {
        /// <summary>
        /// The kind the dropdown lists.
        /// </summary>
        private readonly IObjectKind _kind;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="id">The unique identifier for the dropdown control.</param>
        /// <param name="kind">
        /// The kind descriptor providing the dropdown's label and icon. Cannot be null.
        /// </param>
        /// <param name="serviceUri">
        /// Resolver of the recents/search REST endpoint URI (as a string). Evaluated lazily
        /// per render so the route is resolved after the component graph is wired.
        /// </param>
        public ObjectKindDropdownControl(string id, IObjectKind kind, Func<IRenderControlContext, string> serviceUri)
            : base(id)
        {
            _kind = kind;
            Text = _ => kind.Label;
            Icon = _ => kind.Icon;
            ServiceFactory = renderContext => DataServiceDescriptor.QueryData(serviceUri(renderContext));
        }

        /// <summary>
        /// Converts the control to an HTML representation.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        /// <remarks>
        /// A caller who may read no class of the kind would open an empty menu, so the dropdown
        /// is left out for them (<see cref="WebPermission.ContentVisibility.MayReadAnyOfKind"/>).
        /// </remarks>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (_kind is not null && !WebPermission.ContentVisibility.MayReadAnyOfKind(_kind.Key))
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
