using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
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
    /// The identity block of the reduced object view: the attributes that say which object the
    /// detail pane is showing and who it belongs to - key, kind, class, workspace, assignee.
    /// </summary>
    /// <remarks>
    /// On the full reading view these attributes are spread over the property column
    /// (<c>#wx-content-property</c>), which a detail frame never receives because it embeds the
    /// main content region alone. The reduced view therefore carries them itself, collapsed from
    /// four sections into one read-only list: a pane a few hundred pixels wide has room to say what
    /// the object is, not to offer every way of changing it. The interactive affordances of the
    /// property column - the assign-to-me link, the watcher strip, the workflow split button -
    /// are intentionally absent; the button of
    /// <see cref="ObjectPreviewOpenFragment"/> leads to the view that has them.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Preview>]
    [Order(0)]
    [Cache]
    public sealed class ObjectPreviewPropertyFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;
        private readonly IClassManager _classManager;
        private readonly IIdentityManager _identityManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the current object
        /// from the URL-bound object key.</param>
        /// <param name="classManager">The class manager used to resolve the class name.</param>
        /// <param name="identityManager">The identity manager used to resolve the assignee
        /// display name.</param>
        public ObjectPreviewPropertyFragment
        (
            IFragmentContext fragmentContext,
            IObjectManager objectManager,
            IClassManager classManager,
            IIdentityManager identityManager
        )
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _classManager = classManager;
            _identityManager = identityManager;
        }

        /// <summary>
        /// Renders the identity block. Returns <c>null</c> when the fragment's render conditions
        /// exclude it or when no object can be resolved from the request.
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

            var keyParameter = renderContext?.Request?.GetParameter<ObjectKeyParameter>();
            var @object = _objectManager.GetObjectByKey(keyParameter?.Value);

            if (@object is null)
            {
                return null;
            }

            var section = new ControlSection("object-preview-property-section")
            {
                Header = _ => "kleenestar.core:object.preview.property.header",
                HeaderIcon = _ => new IconCircleInfo(),
                Layout = _ => TypeLayoutSection.Rule
            };

            section.Add(new ControlAttribute("object-preview-key")
            {
                Icon = _ => new IconKey(),
                Key = _ => "kleenestar.core:object.preview.key.label",
                Value = _ => Fallback(@object.Key)
            });

            // the kind is a persisted string key; the catalog turns it into the label the rest
            // of the ui shows, and leaves an add-on kind whose plugin is gone readable as its
            // raw key rather than blank
            var kind = CoreHub.ObjectKindManager.GetKind(@object.Kind);

            section.Add(new ControlAttribute("object-preview-kind")
            {
                Icon = _ => kind?.Icon ?? new IconCube(),
                Key = _ => "kleenestar.core:object.preview.kind.label",
                Value = ctx => kind is null
                    ? Fallback(@object.Kind)
                    : I18N.Translate(ctx, kind.Label)
            });

            var className = _classManager.GetClass(@object.ClassId)?.Name;

            section.Add(new ControlAttribute("object-preview-class")
            {
                Icon = _ => new IconShapes(),
                Key = _ => "kleenestar.core:object.sidebar.class.label",
                Value = _ => Fallback(className)
            });

            section.Add(new ControlAttribute("object-preview-workspace")
            {
                Icon = _ => new IconFolder(),
                Key = _ => "kleenestar.core:object.sidebar.workspace.label",
                Value = _ => Fallback(@object.Workspace?.Name)
            });

            var assigneeName = @object.AssigneeId.HasValue
                ? _identityManager.GetIdentity(@object.AssigneeId.Value)?.Name
                : null;

            section.Add(new ControlAttribute("object-preview-assignee")
            {
                Icon = _ => new IconUserCheck(),
                Key = _ => "kleenestar.core:object.assignee.label",
                Value = ctx => string.IsNullOrWhiteSpace(assigneeName)
                    ? I18N.Translate(ctx, "kleenestar.core:object.assignee.unassigned.label")
                    : assigneeName
            });

            return section.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Returns the supplied text, or the em dash the property sections of the full reading
        /// view use for an attribute that carries no value.
        /// </summary>
        /// <param name="text">The text to show. May be null or blank.</param>
        /// <returns>The text, or an em dash.</returns>
        private static string Fallback(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? "\u2014" : text;
        }
    }
}
