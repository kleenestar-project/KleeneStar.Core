using System;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// A dropdown link that is offered only to a caller it applies to.
    /// </summary>
    /// <remarks>
    /// A dropdown hosted by a cached fragment is built once and shared by every request, so an
    /// entry cannot be added or left out per caller at construction. The entry decides at render
    /// time instead and renders nothing when <see cref="Visible"/> says no - an entry the caller
    /// could only be refused at is not offered. (Passing a filtered item list to
    /// <c>ControlDropdown.Render(…, items)</c> does not work: that overload renders its own items
    /// and ignores the argument.)
    /// </remarks>
    public class ConditionalDropdownItemLink : ControlDropdownItemLink
    {
        /// <summary>
        /// Gets or sets whether the entry is offered to the caller of the rendered request.
        /// Unset means always.
        /// </summary>
        public Func<IRenderControlContext, bool> Visible { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="id">The id of the control.</param>
        public ConditionalDropdownItemLink(string id = null)
            : base(id)
        {
        }

        /// <summary>
        /// Converts the control to an HTML representation, or to nothing when the entry is not
        /// offered to this caller.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node, or <see langword="null"/>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return Visible?.Invoke(renderContext) == false
                ? null
                : base.Render(renderContext, visualTree);
        }
    }
}
