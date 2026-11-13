using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The workflow transitions in the overflow menu of the preview pane: per workflow field a
    /// header with the current state, followed by one entry per state the caller may move the
    /// object to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The preview pane shows no property column, so the status card of the detail page
    /// (<see cref="ObjectPropertyWorkflowCardFragment"/>) is not available there. Both offer the
    /// same targets (<see cref="ObjectWorkflowTargets"/>). An entry links to the transition
    /// endpoint, which redirects back to the referring page (<see cref="WebRestApi.ReturnAddress"/>),
    /// i.e. the overview hosting the pane.
    /// </para>
    /// <para>
    /// The number of entries is known only at render time, so the entries are returned as an
    /// <see cref="HtmlList"/> from one dropdown fragment. Nothing is rendered for a caller
    /// without <see cref="WebPermissions.TransitionExecutePermission"/> on the object or for a
    /// class without a workflow field.
    /// </para>
    /// </remarks>
    [Section<SectionHeadlineMorePreferences>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Preview>]
    [Order(0)]
    [Cache]
    public sealed class ObjectPreviewWorkflowMoreFragment : FragmentControlDropdownItemLink
    {
        private readonly IObjectManager _objectManager;
        private readonly IWorkflowManager _workflowManager;
        private readonly IValueManager _valueManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager resolving the object of the route.</param>
        /// <param name="workflowManager">The workflow manager resolving workflows and states.</param>
        /// <param name="valueManager">The value manager reading the current state.</param>
        public ObjectPreviewWorkflowMoreFragment
        (
            IFragmentContext fragmentContext,
            IObjectManager objectManager,
            IWorkflowManager workflowManager,
            IValueManager valueManager
        )
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _workflowManager = workflowManager;
            _valueManager = valueManager;
        }

        /// <summary>
        /// Renders the header and the target entries of every workflow field.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The entries, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var @object = _objectManager.GetObjectByKey(renderContext?.Request?.GetParameter<ObjectKeyParameter>()?.Value);

            if (@object is null
                || !WebRestApi.ContentAuthorization.MayWrite(@object, renderContext.Request, typeof(WebPermissions.TransitionExecutePermission)))
            {
                return null;
            }

            var nodes = new HtmlList();

            foreach (var field in ObjectWorkflowTargets.Fields(@object))
            {
                var workflow = _workflowManager.GetWorkflowWithStructure(field.WorkflowId.Value);

                if (workflow is null)
                {
                    continue;
                }

                var current = _workflowManager.ResolveStatus(workflow, _valueManager.GetValue(@object.Id, field.Id)?.Data);
                var currentLabel = current?.Name;
                var id = field.Id.ToString("N");

                nodes.Add(new ControlDropdownItemHeader(Id + "-header-" + id)
                {
                    Text = ctx => field.Name + ": " + (currentLabel
                        ?? I18N.Translate(ctx, "kleenestar.core:object.property.workflow.notset.label"))
                }.Render(renderContext, visualTree));

                var targets = ObjectWorkflowTargets.Offered(@object, field, workflow, current);

                if (targets.Count == 0)
                {
                    nodes.Add(new ControlDropdownItemLink(Id + "-none-" + id)
                    {
                        Text = _ => "kleenestar.core:object.property.workflow.target.none",
                        Active = _ => TypeActive.Disabled
                    }.Render(renderContext, visualTree));

                    continue;
                }

                foreach (var (status, transition) in targets)
                {
                    nodes.Add(new ControlDropdownItemLink(Id + "-" + id + "-" + status.Id.ToString("N"))
                    {
                        Text = _ => status.Name,
                        Icon = _ => ObjectPropertyWorkflowCardFragment.ResolveIcon(status),
                        Tooltip = _ => transition?.Name,
                        Uri = _ => ObjectWorkflowTargets.TransitionUri(@object, field, status)
                    }.Render(renderContext, visualTree));
                }
            }

            return nodes.Elements is null || !System.Linq.Enumerable.Any(nodes.Elements) ? null : nodes;
        }
    }
}
