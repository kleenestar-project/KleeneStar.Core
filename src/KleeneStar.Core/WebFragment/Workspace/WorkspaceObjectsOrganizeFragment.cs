using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPolicies;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
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

namespace KleeneStar.Core.WebFragment.Workspace
{
    /// <summary>
    /// Modal content fragment that renders the objects of a workspace as a movable
    /// <see cref="ControlDataTree"/> over <c>/api/1/organize/{workspacekey}</c>
    /// (<see cref="global::KleeneStar.Core.WWW.Api._1_.Organize._workspacekey_.Index"/>), which
    /// loads one level per expansion and re-parents a dropped object.
    /// </summary>
    /// <remarks>
    /// The query parameter <c>select</c> (an object id, set by
    /// <c>ObjectItemOrganizeMoreFragment</c>) selects and reveals that object when the tree opens.
    /// The badge counts the objects of the workspace the caller may read.
    /// </remarks>
    [Title("kleenestar.core:workspace.organize.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Issues._workspacekey_.Organize>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceViewPolicy>>]
    [Cache]
    public sealed class WorkspaceObjectsOrganizeFragment : FragmentControlPanel
    {
        /// <summary>
        /// The query parameter naming the object the tree selects when it opens.
        /// </summary>
        public const string SelectParameter = "select";

        private readonly IObjectManager _objectManager;
        private readonly IWorkspaceManager _workspaceManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to fetch the workspace objects.</param>
        /// <param name="workspaceManager">The workspace manager used to resolve the workspace from the request.</param>
        public WorkspaceObjectsOrganizeFragment(IFragmentContext fragmentContext, IObjectManager objectManager, IWorkspaceManager workspaceManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _workspaceManager = workspaceManager;
        }

        /// <summary>
        /// Renders the organize dialog content. Returns <c>null</c> when the fragment's render conditions
        /// exclude it or when no workspace can be resolved from the request.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var keyParameter = renderContext?.Request?.GetParameter<WorkspaceKeyParameter>();
            var workspace = _workspaceManager.GetWorkspaceByKey(keyParameter?.Value);

            if (workspace is null)
            {
                return null;
            }

            var count = _objectManager.CountObjects(new Query<Model.Entities.Object>()
                .WhereEquals(x => x.WorkspaceId, workspace.Id));

            var tree = new ControlDataTree("workspace-organize-tree")
            {
                Movable = _ => true,
                Selected = context => Guid.TryParse(context?.Request?.GetParameter(SelectParameter)?.Value, out var id)
                    ? id.ToString()
                    : null,
                ServiceFactory = context => DataServiceDescriptor.Data
                (
                    CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Organize._workspacekey_.Index>()?
                        .BindParameters(context.Request)?
                        .ToString()
                )
            };

            // the instruction explains the gesture, not the hierarchy - it belongs above the
            // section rather than inside it, where it would be the first thing folded away
            var help = new ControlText("workspace-organize-help")
            {
                Text = _ => "kleenestar.core:workspace.organize.help",
                Format = _ => TypeFormatText.Paragraph,
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
            };

            // the tree draws its own structure through its indentation, so the section adds no
            // guide line beside it, and the label stays a heading; the badge counts the readable
            // objects of the workspace
            var section = new ControlSection("workspace-organize-section")
            {
                Header = _ => "kleenestar.core:workspace.organize.header",
                HeaderIcon = _ => new IconSitemap(),
                Layout = _ => TypeLayoutSection.Rule,
                Collapsible = _ => false,
                Guide = _ => false,
                Badge = count > 0 ? _ => count.ToString(CultureInfo.InvariantCulture) : null
            };

            if (count == 0)
            {
                section.Add(new ControlText("workspace-organize-empty")
                {
                    Text = _ => "kleenestar.core:workspace.organize.empty",
                    Format = _ => TypeFormatText.Paragraph
                });

                return WrapForModal(renderContext, visualTree, help, section);
            }

            section.Add(tree);

            return WrapForModal(renderContext, visualTree, help, section);
        }

        /// <summary>
        /// Wraps the rendered content in the host element the page-modal extracts. The id must
        /// match the <c>Selector</c> of <c>ObjectOrganizeModalFragment</c> (<c>#kleenestar-organize-content</c>),
        /// whose modal controller copies this element's children into the dialog body - so every
        /// control handed in here arrives in the dialog, in the order it was given.
        /// </summary>
        /// <param name="controls">The controls to host, in reading order.</param>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The host element wrapping the rendered controls.</returns>
        private static IHtmlNode WrapForModal(IRenderControlContext renderContext, IVisualTreeControl visualTree, params IControl[] controls)
        {
            var host = new HtmlElementTextContentDiv()
            {
                Id = "kleenestar-organize-content"
            };

            foreach (var control in controls)
            {
                host.Add(control.Render(renderContext, visualTree));
            }

            return host;
        }
    }
}
