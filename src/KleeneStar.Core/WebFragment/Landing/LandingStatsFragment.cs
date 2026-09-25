using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebRestApi;
using System;
using System.Linq;
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

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// The key-figure row: what concerns the reader - the open issues assigned to them, the
    /// issues they watch, the issues shared with them, and what they did today - each with the
    /// line beneath it that says what the number is doing.
    /// </summary>
    /// <remarks>
    /// The row used to describe the organization (all issues, people, teams, everybody's
    /// activity). Those numbers placed nobody's work anywhere: a reader cannot act on how many
    /// people the installation has, only on what is waiting for them. The figures therefore
    /// follow the personal entries of the sidebar - my issues, shared, watched - so a number
    /// here is always a list one click away.
    /// <para>
    /// A bare number still says little, so every figure carries a second line in the delta of
    /// the tile: how much of the assigned work is in progress, how much of what the reader
    /// follows changed this week, how long ago they last did something. The counts go through
    /// <see cref="IObjectManager"/>, so security levels and content visibility bite as on every
    /// list - a figure never counts an issue its list would not show.
    /// </para>
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Condition<global::KleeneStar.Core.WebIdentity.SignedInCondition>]
    [Scope<global::KleeneStar.Core.WWW.Index>]
    [Order(20)]
    public sealed class LandingStatsFragment : FragmentControlPanel
    {
        /// <summary>
        /// The window the "changed this week" notes look back over.
        /// </summary>
        private static readonly TimeSpan Week = TimeSpan.FromDays(7);

        private readonly IObjectManager _objectManager;
        private readonly IWatcherManager _watcherManager;
        private readonly IShareManager _shareManager;
        private readonly IAuditManager _auditManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to count the issues.</param>
        /// <param name="watcherManager">The watcher manager naming the watched issues.</param>
        /// <param name="shareManager">The share manager naming the issues shared with the reader.</param>
        /// <param name="auditManager">The audit manager used to describe the reader's activity.</param>
        public LandingStatsFragment
        (
            IFragmentContext fragmentContext,
            IObjectManager objectManager,
            IWatcherManager watcherManager,
            IShareManager shareManager,
            IAuditManager auditManager
        )
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _watcherManager = watcherManager;
            _shareManager = shareManager;
            _auditManager = auditManager;
        }

        /// <summary>
        /// Renders the key-figure row. Returns <c>null</c> when the fragment's render
        /// conditions exclude it or nobody is signed in.
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

            var identityId = CoreHub.SessionManager?.GetCurrentIdentityId(renderContext?.Request) ?? Guid.Empty;

            if (identityId == Guid.Empty)
            {
                return null;
            }

            var row = new ControlGroup
            (
                "landing-stats",
                BuildAssigned(renderContext, identityId),
                BuildFollowed
                (
                    renderContext,
                    "watched",
                    "kleenestar.core:landing.stats.watched.label",
                    new IconEye(),
                    LandingScope.GetWatchedIds(_watcherManager, identityId)
                ),
                BuildFollowed
                (
                    renderContext,
                    "shared",
                    "kleenestar.core:landing.stats.shared.label",
                    new IconShareNodes(),
                    LandingScope.GetSharedIds(_shareManager, identityId)
                ),
                BuildActivity(renderContext, identityId)
            )
            {
                Classes = ["ks-landing-stats"]
            };

            return row.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Builds a single field of the row.
        /// </summary>
        /// <param name="key">The short id suffix of the field.</param>
        /// <param name="label">The label, already translated.</param>
        /// <param name="icon">The icon beside the label.</param>
        /// <param name="value">The figure.</param>
        /// <param name="note">The note beneath the figure, already composed.</param>
        /// <param name="trend">The direction the note describes, which colours it.</param>
        /// <returns>The tile.</returns>
        private static ControlStat BuildStat
        (
            string key,
            string label,
            IIcon icon,
            string value,
            string note,
            TypeStatTrend trend = TypeStatTrend.Neutral
        )
        {
            return new ControlStat("landing-stat-" + key)
            {
                Label = _ => label,
                Value = _ => value,
                Delta = _ => note,
                Trend = _ => trend,
                Icon = _ => icon
            };
        }

        /// <summary>
        /// Builds the assigned field: the open issues assigned to the reader, and how many of
        /// them are in progress.
        /// </summary>
        /// <remarks>
        /// "Open" is the definition of the *My work* section (<see cref="LandingWorkSection.GetOpen"/>):
        /// the state is a value row, so the open ones are found by scanning, bounded by
        /// <see cref="LandingWorkSection.ScanLimit"/>. A reader with more assigned issues than
        /// that sees the figure marked as a lower bound rather than a wrong exact number.
        /// </remarks>
        /// <param name="renderContext">The render context.</param>
        /// <param name="identityId">The reader.</param>
        /// <returns>The tile.</returns>
        private ControlStat BuildAssigned(IRenderControlContext renderContext, Guid identityId)
        {
            var open = LandingWorkSection.GetOpen(_objectManager, identityId, LandingWorkSection.ScanLimit);
            var progress = open.Count(x => ObjectBoardProjection.Normalize(x.Category?.Name) == "inprogress");

            var assigned = _objectManager.CountObjects(new WebExpress.WebIndex.Queries.Query<Model.Entities.Object>()
                .WhereEquals(x => x.Kind, Model.Entities.ObjectKind.Issue)
                .Where(x => x.State == Model.Entities.WorkspaceState.Active)
                .Where(x => x.AssigneeId == identityId));

            var value = LandingHtml.Number(open.Count, renderContext)
                + (assigned > LandingWorkSection.ScanLimit ? "+" : string.Empty);

            var note = progress > 0
                ? LandingHtml.Count(renderContext, "kleenestar.core:landing.stats.assigned.progress", progress)
                : I18N.Translate(renderContext, "kleenestar.core:landing.stats.assigned.idle");

            return BuildStat
            (
                "assigned",
                I18N.Translate(renderContext, "kleenestar.core:landing.stats.assigned.label"),
                new IconListCheck(),
                value,
                note
            );
        }

        /// <summary>
        /// Builds a field over a set of issues the reader follows - watched or shared with them:
        /// how many there are, and how many of them changed this week.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="key">The short id suffix of the field.</param>
        /// <param name="label">The internationalization key of the label.</param>
        /// <param name="icon">The icon beside the label.</param>
        /// <param name="ids">The ids of the followed objects.</param>
        /// <returns>The tile.</returns>
        private ControlStat BuildFollowed(IRenderControlContext renderContext, string key, string label, IIcon icon, Guid[] ids)
        {
            var since = DateTime.UtcNow - Week;

            // an empty id set is answered without a query: nothing is followed, nothing changed
            var total = ids.Length == 0 ? 0 : _objectManager.CountObjects(LandingScope.BuildIdQuery(ids));
            var changed = total == 0 ? 0 : _objectManager.CountObjects(LandingScope.BuildIdQuery(ids).Where(x => x.Updated >= since));

            var note = changed > 0
                ? LandingHtml.Count(renderContext, "kleenestar.core:landing.stats.changed", changed)
                : I18N.Translate(renderContext, "kleenestar.core:landing.stats.unchanged");

            return BuildStat
            (
                key,
                I18N.Translate(renderContext, label),
                icon,
                LandingHtml.Number(total, renderContext),
                note,
                changed > 0 ? TypeStatTrend.Up : TypeStatTrend.Neutral
            );
        }

        /// <summary>
        /// Builds the activity field: what the reader worked on today, and how long ago the last
        /// of it was.
        /// </summary>
        /// <remarks>
        /// It counts the events the activity list shows (<see cref="LandingActivitySection.WorkQuery"/>),
        /// narrowed to the reader as the actor - signing in and the installation starting are
        /// no work.
        /// </remarks>
        /// <param name="renderContext">The render context.</param>
        /// <param name="identityId">The reader.</param>
        /// <returns>The tile.</returns>
        private ControlStat BuildActivity(IRenderControlContext renderContext, Guid identityId)
        {
            var since = DateTime.UtcNow.Date;
            var today = _auditManager.CountEvents(LandingActivitySection.WorkQuery()
                .Where(x => x.ActorId == identityId)
                .Where(x => x.Timestamp >= since));

            var latest = _auditManager
                .GetEvents(LandingActivitySection.WorkQuery()
                    .Where(x => x.ActorId == identityId)
                    .WithPaging(0, 1))
                .FirstOrDefault();

            var note = latest is null
                ? I18N.Translate(renderContext, "kleenestar.core:landing.stats.activity.none")
                : LandingHtml.Join
                (
                    I18N.Translate(renderContext, "kleenestar.core:landing.stats.activity.last"),
                    LandingHtml.Age(latest.Timestamp, renderContext)
                );

            return BuildStat
            (
                "activity",
                I18N.Translate(renderContext, "kleenestar.core:landing.stats.activity.label"),
                new IconBolt(),
                LandingHtml.Number(today, renderContext),
                note
            );
        }
    }
}
