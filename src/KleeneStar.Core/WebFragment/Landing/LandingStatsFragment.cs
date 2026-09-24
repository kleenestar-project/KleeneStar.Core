using KleeneStar.Core.WebManager;
using System;
using System.Linq;
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

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// The key-figure row: how many issues the installation holds, how many people take part,
    /// how many teams are active, and how much happened today - each with the line beneath it
    /// that says what the number is doing.
    /// </summary>
    /// <remarks>
    /// A bare number says little. "112" next to "8 new this week" says whether the queue is
    /// growing; "4" next to "IT, Dev, HR, Finance" says which teams those are. The second line
    /// is what turns the row from a scoreboard into orientation, which is the whole point of
    /// the page. It is carried by the delta of the tile, which is the field that exists for
    /// exactly this.
    /// <para>
    /// The figures are counted, never loaded - each is a single <c>COUNT</c> against a filtered
    /// set - because the landing page is hit by everybody at the start of every session and
    /// must not drag a table across to print a number. They describe the organization rather
    /// than the caller; the caller's own work is the first section of the wide column.
    /// </para>
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Condition<global::KleeneStar.Core.WebIdentity.SignedInCondition>]
    [Scope<global::KleeneStar.Core.WWW.Index>]
    [Order(20)]
    public sealed class LandingStatsFragment : FragmentControlPanel
    {
        /// <summary>
        /// The window the "new this week" and "active this week" notes look back over.
        /// </summary>
        private static readonly TimeSpan Week = TimeSpan.FromDays(7);

        /// <summary>
        /// The number of team names listed beneath the team count before the note is cut short.
        /// </summary>
        private const int TeamNames = 4;

        private readonly IObjectManager _objectManager;
        private readonly IIdentityManager _identityManager;
        private readonly IGroupManager _groupManager;
        private readonly IAuditManager _auditManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to count the issues.</param>
        /// <param name="identityManager">The identity manager used to count the people.</param>
        /// <param name="groupManager">The group manager used to count and name the teams.</param>
        /// <param name="auditManager">The audit manager used to describe the activity.</param>
        public LandingStatsFragment
        (
            IFragmentContext fragmentContext,
            IObjectManager objectManager,
            IIdentityManager identityManager,
            IGroupManager groupManager,
            IAuditManager auditManager
        )
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _identityManager = identityManager;
            _groupManager = groupManager;
            _auditManager = auditManager;
        }

        /// <summary>
        /// Renders the key-figure row. Returns <c>null</c> when the fragment's render
        /// conditions exclude it.
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

            var row = new ControlGroup
            (
                "landing-stats",
                BuildIssues(renderContext),
                BuildPeople(renderContext),
                BuildTeams(renderContext),
                BuildActivity(renderContext)
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
        /// Builds the issue field: the active issues, and how many of them were raised this week.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The tile.</returns>
        private ControlStat BuildIssues(IRenderControlContext renderContext)
        {
            var total = _objectManager.CountObjects(BuildIssueQuery());

            var since = DateTime.UtcNow - Week;
            var fresh = _objectManager.CountObjects(BuildIssueQuery().Where(x => x.Created >= since));

            var note = fresh > 0
                ? LandingHtml.Count(renderContext, "kleenestar.core:landing.stats.issues.new", fresh)
                : I18N.Translate(renderContext, "kleenestar.core:landing.stats.issues.none");

            return BuildStat
            (
                "issues",
                I18N.Translate(renderContext, "kleenestar.core:landing.stats.issues.label"),
                new IconListCheck(),
                LandingHtml.Number(total, renderContext),
                note,
                fresh > 0 ? TypeStatTrend.Up : TypeStatTrend.Neutral
            );
        }

        /// <summary>
        /// Builds the people field: the active identities, and how many of them left a trace in
        /// the audit log this week.
        /// </summary>
        /// <remarks>
        /// "Active this week" is read from the audit log rather than from a session table
        /// because a session says somebody was signed in, not that they did anything. The
        /// actors of the week are the people the organization actually heard from.
        /// </remarks>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The tile.</returns>
        private ControlStat BuildPeople(IRenderControlContext renderContext)
        {
            var query = new Query<Model.Entities.Identity>()
                .Where(x => x.State == Model.Entities.IdentityState.Active);

            var total = _identityManager.CountIdentities(query);
            var active = CountRecentActors();

            var note = active > 0
                ? LandingHtml.Count(renderContext, "kleenestar.core:landing.stats.people.active", active)
                : I18N.Translate(renderContext, "kleenestar.core:landing.stats.people.none");

            return BuildStat
            (
                "people",
                I18N.Translate(renderContext, "kleenestar.core:landing.stats.people.label"),
                new IconUsers(),
                LandingHtml.Number(total, renderContext),
                note
            );
        }

        /// <summary>
        /// Builds the team field: the active groups, named as far as the line allows.
        /// </summary>
        /// <remarks>
        /// The implicit groups (<see cref="Model.Entities.Group.IsImplicit(Guid)"/> - everybody
        /// signed in, everybody who is not) are rows of the group table so permissions can name
        /// them, but they are no team anybody belongs to by choice, and counting them put
        /// "Anonymous" among the teams of the organization.
        /// </remarks>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The tile.</returns>
        private ControlStat BuildTeams(IRenderControlContext renderContext)
        {
            var query = new Query<Model.Entities.Group>()
                .Where(x => x.State == Model.Entities.GroupState.Active)
                .OrderByAsc(x => x.Name);

            var groups = _groupManager.GetGroups(query)
                .Where(x => !Model.Entities.Group.IsImplicit(x.Id))
                .ToList();
            var names = groups.Take(TeamNames).Select(x => x.Name).ToArray();

            var note = string.Join(", ", names);

            if (groups.Count > TeamNames)
            {
                note = LandingHtml.Join(note, LandingHtml.Count(renderContext, "kleenestar.core:landing.stats.teams.more", groups.Count - TeamNames));
            }

            return BuildStat
            (
                "teams",
                I18N.Translate(renderContext, "kleenestar.core:landing.stats.teams.label"),
                new IconUserGroup(),
                LandingHtml.Number(groups.Count, renderContext),
                note
            );
        }

        /// <summary>
        /// Builds the activity field: what people worked on today, and how long ago the last of
        /// it was.
        /// </summary>
        /// <remarks>
        /// It counts the events the activity list shows (<see cref="LandingActivitySection.WorkQuery"/>),
        /// not the whole audit log - that one is mostly the installation starting and people
        /// signing in, and a figure of hundreds on a quiet day said nothing about the work.
        /// </remarks>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The tile.</returns>
        private ControlStat BuildActivity(IRenderControlContext renderContext)
        {
            var since = DateTime.UtcNow.Date;
            var today = _auditManager.CountEvents(LandingActivitySection.WorkQuery().Where(x => x.Timestamp >= since));

            var latest = _auditManager
                .GetEvents(LandingActivitySection.WorkQuery().WithPaging(0, 1))
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

        /// <summary>
        /// Counts the distinct identities that caused an audit event within the week.
        /// </summary>
        /// <remarks>
        /// Distinctness cannot be expressed in the query, so the actor ids of the window are
        /// read and reduced here. The window is a week of one installation's events, which is
        /// the scale this stays reasonable at.
        /// </remarks>
        /// <returns>The number of distinct actors.</returns>
        private int CountRecentActors()
        {
            var since = DateTime.UtcNow - Week;

            var query = new Query<Model.Entities.AuditEvent>()
                .Where(x => x.Timestamp >= since)
                .Where(x => x.ActorId != null);

            return _auditManager
                .GetEvents(query)
                .Select(x => x.ActorId)
                .Distinct()
                .Count();
        }

        /// <summary>
        /// Builds a fresh query over the active issues - fresh every time, because a query that
        /// already carries a filter or paging would narrow the next count as well.
        /// </summary>
        /// <returns>The query.</returns>
        private static IQuery<Model.Entities.Object> BuildIssueQuery()
        {
            return new Query<Model.Entities.Object>()
                .WhereEquals(x => x.Kind, Model.Entities.ObjectKind.Issue)
                .Where(x => x.State == Model.Entities.WorkspaceState.Active);
        }
    }
}
