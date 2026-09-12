using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebPermissions;
using KleeneStar.Core.WebPolicies;
using System;
using System.Globalization;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;
using CommitEntity = KleeneStar.Model.Entities.Commit;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The restore button below a revision in the history dialog, together with the note
    /// explaining what restoring does — or, for the head, why it is not offered.
    /// </summary>
    /// <remarks>
    /// The button is a fragment of its own rather than part of
    /// <see cref="ObjectHistoryDetailFragment"/> so it can carry the object <b>edit</b> policy
    /// while the revision beside it carries the view policy: reading a history and writing one of
    /// its states back are different grants (<c>object_read_history</c> against
    /// <c>object_restore_state</c>), and a user holding only the first must see the revision
    /// without being offered a way to reapply it. The policy attribute documents that; what
    /// withholds the button is the same <see cref="PageAuthorization"/> question the restore
    /// page asks, so the offer and the act cannot disagree.
    /// </remarks>
    [Section<SectionContentSecondary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.HistoryDetail>]
    [Policy<ObjectEditPolicy>]
    [Cache]
    public sealed class ObjectHistoryRestoreFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;
        private readonly ICommitManager _commitManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the object from the request.</param>
        /// <param name="commitManager">The commit manager the revision is read from.</param>
        public ObjectHistoryRestoreFragment(IFragmentContext fragmentContext, IObjectManager objectManager, ICommitManager commitManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _commitManager = commitManager;
        }

        /// <summary>
        /// Renders the restore button, or the note explaining why the revision cannot be
        /// restored. Returns <c>null</c> when the fragment's render conditions exclude it or when
        /// neither the object nor the revision can be resolved.
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

            var @object = _objectManager.GetObjectByKey(renderContext?.Request?.GetParameter<ObjectKeyParameter>());
            var commit = @object is null ? null : Resolve(@object, renderContext);

            if (commit is null)
            {
                return null;
            }

            // a caller the restore page would refuse is not offered the button
            if (!PageAuthorization.IsGranted(renderContext?.Request, typeof(ObjectRestoreStatePermission), PageAuthorization.ChainOf(@object)))
            {
                return null;
            }

            var state = _commitManager.GetStateAt(@object.Id, commit.Number);

            if (state is null)
            {
                return null;
            }

            var panel = new ControlPanel("object-history-restore");

            // restoring the head would append a commit that records no change; saying so is more
            // useful than a button that appears to do nothing
            if (state.IsHead)
            {
                panel.Add(new ControlText()
                {
                    Text = _ => I18N.Translate(renderContext, "kleenestar.core:object.history.restore.head"),
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                    Format = _ => TypeFormatText.Small
                });

                return panel.Render(renderContext, visualTree);
            }

            var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.HistoryRestore>()
                .BindParameters(new ObjectKeyParameter(@object.Key))
                .BindParameters(renderContext?.Request)
                .Add(new UriQuery(global::KleeneStar.Core.WWW.Issue._objectkey_.HistoryDetail.CommitParameter, commit.Number.ToString(CultureInfo.InvariantCulture)));

            panel.Add(new ControlButtonLink("object-history-restore-button")
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:object.history.restore.label"),
                Icon = _ => new IconArrowRotateLeft(),
                Outline = _ => true,
                Uri = _ => uri,
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Two, PropertySpacing.Space.None)
            });

            panel.Add(new ControlText()
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:object.history.restore.hint"),
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                Format = _ => TypeFormatText.Small
            });

            return panel.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Resolves the revision addressed by the <c>?commit=</c> query, which carries either the
        /// revision number or the commit id. Falls back to the head, matching the revision the
        /// detail fragment beside it shows.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="renderContext">The render context carrying the request.</param>
        /// <returns>The commit, or <c>null</c>.</returns>
        private CommitEntity Resolve(ObjectEntity @object, IRenderControlContext renderContext)
        {
            var raw = renderContext?.Request?.GetParameter(global::KleeneStar.Core.WWW.Issue._objectkey_.HistoryDetail.CommitParameter)?.Value;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return _commitManager.GetHead(@object.Id);
            }

            if (Guid.TryParse(raw, out var commitId))
            {
                var byId = _commitManager.GetCommit(commitId);

                return byId?.ObjectId == @object.Id ? byId : null;
            }

            return int.TryParse(raw.TrimStart('#'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                ? _commitManager.GetCommit(@object.Id, number)
                : null;
        }
    }
}
