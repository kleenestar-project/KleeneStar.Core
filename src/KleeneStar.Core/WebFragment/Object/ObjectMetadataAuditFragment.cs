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
    /// Headline-metadata fragment that renders the audit trail of the current object in plain
    /// text - who created it, who last updated it, and when the last update happened - on the
    /// reading views and on the reduced view <see cref="WWW.Issue._objectkey_.Preview"/> a
    /// master-detail pane shows.
    /// </summary>
    /// <remarks>
    /// Creator and updater are resolved from the object's
    /// <see cref="Model.Entities.Object.CreatorId"/> / <see cref="Model.Entities.Object.UpdaterId"/>
    /// through the <see cref="IIdentityManager"/> (an unknown identity renders as an em dash).
    /// The timestamp is taken from <see cref="Model.Entities.Object.Updated"/>. The fragment is
    /// purely informational and carries no interactive controls, which is why the reduced view
    /// takes it as it stands: three small lines are the whole of what the lifecycle card of the
    /// property column would have said in a pane that never receives that column.
    /// </remarks>
    [Section<SectionHeadlineMetadata>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Preview>]
    [Order(1)]
    [Cache]
    public sealed class ObjectMetadataAuditFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;
        private readonly IIdentityManager _identityManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the current
        /// object from the URL-bound object key.</param>
        /// <param name="identityManager">The identity manager used to resolve the creator
        /// and updater display names.</param>
        public ObjectMetadataAuditFragment(IFragmentContext fragmentContext, IObjectManager objectManager, IIdentityManager identityManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _identityManager = identityManager;
        }

        /// <summary>
        /// Renders the creator / updater / last-update text for the current object. Returns
        /// <c>null</c> when the fragment's render conditions exclude it or when no object can
        /// be resolved from the request.
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

            var creatorName = ResolveName(@object.CreatorId);
            var updaterName = ResolveName(@object.UpdaterId);
            var updated = @object.Updated.ToString("yyyy-MM-dd HH:mm");

            var panel = new ControlPanel("object-metadata-audit");

            panel.Add(BuildLine("object-metadata-audit-creator", "kleenestar.core:object.creator.label", creatorName));
            panel.Add(BuildLine("object-metadata-audit-updater", "kleenestar.core:object.updater.label", updaterName));
            panel.Add(BuildLine("object-metadata-audit-updated", "kleenestar.core:object.lastupdate.label", updated));

            return panel.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Builds a single small "label value" text line, translating the label key against
        /// the render context.
        /// </summary>
        /// <param name="id">The control id.</param>
        /// <param name="labelKey">The i18n key of the leading label.</param>
        /// <param name="value">The already-resolved value to append after the label.</param>
        /// <returns>The text control for the line.</returns>
        private static IControl BuildLine(string id, string labelKey, string value)
        {
            return new ControlText(id)
            {
                Text = ctx => $"{I18N.Translate(ctx, labelKey)} {value}",
                Format = _ => TypeFormatText.Small,
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
            };
        }

        /// <summary>
        /// Resolves the display name of the identity with the supplied id, or an em dash
        /// when the id is <c>null</c> or the identity cannot be resolved.
        /// </summary>
        /// <param name="identityId">The identity id, or <c>null</c>.</param>
        /// <returns>The display name, or an em dash.</returns>
        private string ResolveName(System.Guid? identityId)
        {
            if (!identityId.HasValue)
            {
                return "—";
            }

            var name = _identityManager.GetIdentity(identityId.Value)?.Name;

            return string.IsNullOrWhiteSpace(name) ? "—" : name;
        }
    }
}
