using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Group
{
    /// <summary>
    /// Represents a edit form fragment for a group.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Settings.Group._groupid_.Edit>]
    [Cache]
    public sealed class GroupEditFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the input text control for specifying the name of the group.
        /// </summary>
        public ControlDataFormItemInputUnique GroupName { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Group.Name),
            Label = _ => "kleenestar.core:setting.group.name.label",
            Placeholder = _ => "kleenestar.core:setting.group.name.placeholder",
            Help = _ => "kleenestar.core:setting.group.name.help",
            Required = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Groups.UniqueName>().ToString())};

        /// <summary>
        /// Gets the input text control for specifying the description of the group.
        /// </summary>
        public ControlFormItemInputText Description { get; } = new ControlFormItemInputText()
        {
            Name = _ => nameof(Model.Entities.Group.Description),
            Label = _ => "kleenestar.core:setting.group.description.label",
            Placeholder = _ => "kleenestar.core:setting.group.description.placeholder",
            Format = _ => TypeEditTextFormat.Wysiwyg,
            LinkLibraryUri = CoreHub.EditorLinkLibrary,
            Required = _ => false
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public GroupEditFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(GroupName);
            Add(Description);
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Groups.Index>();
            ItemId = renderContext =>
            {
                var groupId = renderContext.Request.GetParameter<GroupIdParameter>();
                return groupId?.Value?.ToString();
            };
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
