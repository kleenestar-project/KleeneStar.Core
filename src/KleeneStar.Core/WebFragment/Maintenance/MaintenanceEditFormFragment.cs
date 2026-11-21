using KleeneStar.Core.WebControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Maintenance
{
    /// <summary>
    /// Represents the form on the maintenance settings page with which the instruction text shown
    /// to every user as a toast is written and switched on or off.
    /// </summary>
    /// <remarks>
    /// The notice is a singleton, so the form addresses the fixed record rather than reading an id
    /// from the route the way the other edit forms do.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Settings.Maintenance>]
    [Cache]
    public sealed class MaintenanceEditFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the toggle that decides whether the instruction text is shown to the users.
        /// </summary>
        public ControlFormItemInputCheck Enabled { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Maintenance.Enabled),
            Label = _ => "kleenestar.core:setting.maintenance.enabled.label",
            Help = _ => "kleenestar.core:setting.maintenance.enabled.help",
            Layout = _ => TypeLayoutCheck.Switch
        };

        /// <summary>
        /// Gets the input control for the instruction text.
        /// </summary>
        /// <remarks>
        /// The text is edited in the rich editor, like the other description fields, so the notice
        /// can carry emphasis and a link. The toast renders the stored markup accordingly.
        /// </remarks>
        public ControlFormItemInputText Message { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Maintenance.Message),
            Label = _ => "kleenestar.core:setting.maintenance.message.label",
            Placeholder = _ => "kleenestar.core:setting.maintenance.message.placeholder",
            Help = _ => "kleenestar.core:setting.maintenance.message.help",
            Format = _ => TypeEditTextFormat.Wysiwyg,
            LinkLibraryUri = CoreHub.EditorLinkLibrary,
            Required = _ => false
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public MaintenanceEditFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(Enabled);
            Add(Message);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Maintenance.Index>();

            ItemId = _ => Model.Entities.Maintenance.SingletonId.ToString();
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
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}
