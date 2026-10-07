using System;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// Shared base of the sidebar links that connect the landing page and the three
    /// entry-path pages, so a reader can move between what is mine, what is shared and what
    /// is watched without going back through the landing page each time.
    /// </summary>
    /// <remarks>
    /// A concrete subclass fixes the destination, the label and the icon, and scopes itself
    /// to the pages the link appears on. The link marks itself active when the request is
    /// already on its destination - compared by path segments, the way the class sidebar
    /// does it.
    /// </remarks>
    public abstract class LandingSidebarLinkFragment : FragmentControlSidebarItemLink
    {
        /// <summary>
        /// Gets the route the link points at.
        /// </summary>
        protected new abstract IUri Target { get; }

        /// <summary>
        /// Gets the resource key of the link label.
        /// </summary>
        protected abstract string Label { get; }

        /// <summary>
        /// Gets the icon of the link.
        /// </summary>
        protected abstract IIcon Symbol { get; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services
        /// for its operation. Cannot be null.
        /// </param>
        protected LandingSidebarLinkFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Icon = _ => Symbol;
            Text = _ => Label;
            Uri = _ => Target;
            Active = renderContext => IsActive(renderContext)
                ? TypeActive.Active
                : TypeActive.None;
        }

        /// <summary>
        /// Renders the link. Returns <c>null</c> when the fragment's render conditions
        /// exclude it.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>The HTML node, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Returns whether the request is already on the link's destination.
        /// </summary>
        /// <param name="renderContext">The render context carrying the request.</param>
        /// <returns><see langword="true"/> when the link points at the current page.</returns>
        private bool IsActive(IRenderControlContext renderContext)
        {
            var target = string.Join("/", Target?.PathSegments ?? []);
            var current = string.Join("/", renderContext?.Request?.Uri?.PathSegments ?? []);

            return !string.IsNullOrEmpty(target)
                && string.Equals(current, target, StringComparison.OrdinalIgnoreCase);
        }
    }
}
