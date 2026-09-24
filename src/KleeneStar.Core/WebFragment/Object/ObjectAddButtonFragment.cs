using KleeneStar.Core.WebParameter;
using System;
using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Represents a control fragment that provides a button link for adding a new object within the workspace.
    /// </summary>
    [Section<SectionAppQuickcreatePreferences>]
    [Condition<global::KleeneStar.Core.WebIdentity.SignedInCondition>]
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class ObjectAddButtonFragment : FragmentControlSplitButtonItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for its operation. 
        /// Cannot be null.
        /// </param>
        public ObjectAddButtonFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.core:object.add.label";
            Icon = _ => new IconPlus();
            Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two);
            BackgroundColor = _ => new PropertyColorBackground(TypeColorBackground.Highlight);
            PrimaryAction = ctx => new ActionModal
            (
                "modal-form",
                AddUri(ctx),
                TypeModalSize.ExtraLarge
            );
        }

        /// <summary>
        /// Returns the address of the create wizard - naming the open document when the page
        /// shows one, so a document created from here is placed below it.
        /// </summary>
        /// <param name="renderContext">The context of the page the button stands on.</param>
        /// <returns>The address.</returns>
        private static IUri AddUri(IRenderControlContext renderContext)
        {
            // a fresh uri per call: the sitemap builds one each time, so the query added below
            // accumulates nowhere
            var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Objects.Add>();
            var key = renderContext?.Request?.GetParameter<ObjectKeyParameter>()?.Value;
            var open = string.IsNullOrWhiteSpace(key) ? null : CoreHub.ObjectManager.GetObjectByKey(key);

            return string.Equals(open?.Kind, Model.Entities.ObjectKind.Document, StringComparison.OrdinalIgnoreCase)
                ? uri?.Add(new UriQuery(ObjectAddFormFragment.ParentParameter, open.Key))
                : uri;
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">
        /// The context in which the control is rendered.
        /// </param>
        /// <param name="visualTree">
        /// The visual tree representing the control's structure.
        /// </param>
        /// <returns>
        /// An HTML node representing the rendered control.
        /// </returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}
