using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    using AssigneeApi = global::KleeneStar.Core.WWW.Api._1_.Assignee._objectkey_.Index;

    /// <summary>
    /// Object-scoped property card that shows the people of the current object on the issue and
    /// asset pages, one <see cref="ObjectPersonRow"/> each: who is responsible, who created it,
    /// who decides about it and who watches it.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><b>Assignee</b> first, because it answers "who works on this": the framework's
    ///   <see cref="ControlDataAssignment"/> over <see cref="WWW.Api._1_.Assignee._objectkey_.Index"/>,
    ///   seeded with the stored assignment. A signed-in caller who may change the object gets
    ///   <i>assign to me</i> or <i>unassign</i> at the end of the row and can search for another
    ///   assignee (<see cref="WWW.Api._1_.WatcherUsers._objectkey_.Index"/>); the row is redrawn
    ///   from the answer, without reloading the page.</item>
    ///   <item><b>Creator</b>, when the object names one.</item>
    ///   <item><b>Approvers</b>, only when the object has an approval: the pending one, else the
    ///   latest that was not cancelled. Each approver who decided carries the verdict, each account
    ///   and group of the open step carries <i>pending</i>; the role line names the step. Without an
    ///   approval nothing is shown, not even a heading.</item>
    ///   <item><b>Watchers</b>: the framework's <see cref="ControlDataWatcher"/> over
    ///   <see cref="WWW.Api._1_.Watchers._objectkey_.Index"/> and
    ///   <see cref="WWW.Api._1_.WatcherUsers._objectkey_.Index"/>.</item>
    /// </list>
    /// </remarks>
    [Section<SectionPropertyPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WebScope.IScopeObjectKindDetail>]
    [Order(5)]
    [Cache]
    public sealed class ObjectPropertyPeopleCardFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the current
        /// object from the URL-bound object key.</param>
        public ObjectPropertyPeopleCardFragment(IFragmentContext fragmentContext, IObjectManager objectManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
        }

        /// <summary>
        /// Renders the people card for the current object. Returns <c>null</c> when the
        /// fragment's render conditions exclude it or when no object can be resolved from
        /// the request.
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

            var request = renderContext?.Request;
            var @object = _objectManager.GetObjectByKey(request?.GetParameter<ObjectKeyParameter>()?.Value);

            if (@object is null)
            {
                return null;
            }

            var section = new ControlSection("object-property-people-section")
            {
                Header = _ => "kleenestar.core:object.property.people.header",
                HeaderIcon = _ => new IconUsers(),
                Layout = _ => TypeLayoutSection.Rule
            };

            var people = new ControlPanel("object-property-people")
            {
                Classes = ["ks-people"]
            };

            people.Add(BuildAssignee(@object, request));

            if (Person("object-property-creator", @object.CreatorId, "kleenestar.core:object.creator.label") is { } creator)
            {
                people.Add(creator);
            }

            var approvers = BuildApprovers(@object).ToList();

            if (approvers.Count > 0)
            {
                people.Add(Heading("object-property-approvers-heading", "kleenestar.core:object.property.people.approvers"));
                people.Add([.. approvers]);
            }

            people.Add(Heading("object-property-watcher-label", "kleenestar.core:object.property.watcher.header"));
            people.Add(new ControlDataWatcher("object-property-watcher")
            {
                MaxVisible = _ => 6
            }
                .DataService<global::KleeneStar.Core.WWW.Api._1_.Watchers._objectkey_.Index>()
                .UsersService<global::KleeneStar.Core.WWW.Api._1_.WatcherUsers._objectkey_.Index>());

            section.Add(people);

            return section.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Builds the assignee row, seeded with the stored assignment so it renders without a
        /// round trip.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="request">The request.</param>
        /// <returns>The row.</returns>
        private static ControlDataAssignment BuildAssignee(Model.Entities.Object @object, IRequest request)
        {
            var assignee = AssigneeApi.ToItem(@object.AssigneeId);
            var self = AssigneeApi.ToItem(CoreHub.SessionManager.GetCurrentIdentityId(request));
            var mayChange = AssigneeApi.MayChange(@object, request);

            return new ControlDataAssignment("object-property-assignee")
            {
                Caption = _ => "kleenestar.core:object.assignee.label",
                Placeholder = _ => "kleenestar.core:object.assignee.unassigned.label",
                AssignToMeText = _ => "kleenestar.core:object.assignee.assignme.label",
                UnassignText = _ => "kleenestar.core:object.assignee.unassign.label",
                AssignToText = _ => "kleenestar.core:object.assignee.assignto.label",
                Assignee = _ => assignee,
                Self = _ => self,
                MayChange = _ => mayChange
            }
                .DataService<AssigneeApi>()
                .UsersService<global::KleeneStar.Core.WWW.Api._1_.WatcherUsers._objectkey_.Index>();
        }

        /// <summary>
        /// Builds the approver rows of the object's approval: the pending one, else the latest
        /// that was not cancelled.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <returns>The rows; empty without an approval.</returns>
        private static IEnumerable<ObjectPersonRow> BuildApprovers(Model.Entities.Object @object)
        {
            var approvals = CoreHub.ApprovalManager.GetApprovals(@object.Id)
                .Where(x => x.State != ApprovalState.Cancelled)
                .ToList();
            var approval = approvals.FirstOrDefault(x => x.State == ApprovalState.Pending)
                ?? approvals.OrderByDescending(x => x.Requested).FirstOrDefault();

            if (approval is null)
            {
                yield break;
            }

            var steps = CoreHub.ApprovalManager.StepsOf(approval);
            string StepName(int index) => index >= 0 && index < steps.Count ? steps[index].Name : null;

            // the latest decision of an approver per step stands
            var decisions = (approval.Decisions ?? [])
                .GroupBy(x => (x.Step, x.IdentityId))
                .Select(x => x.OrderByDescending(d => d.Decided).First())
                .OrderBy(x => x.Step)
                .ThenBy(x => x.Decided)
                .ToList();

            foreach (var decision in decisions)
            {
                if (Person($"object-property-approver-{decision.Step}-{decision.IdentityId:N}", decision.IdentityId, StepName(decision.Step)) is { } row)
                {
                    row.Status = decision.Verdict == ApprovalVerdict.Approve
                        ? "kleenestar.core:object.property.people.approved"
                        : "kleenestar.core:object.property.people.rejected";
                    row.StatusTone = decision.Verdict == ApprovalVerdict.Approve ? "success" : "danger";

                    yield return row;
                }
            }

            if (approval.State != ApprovalState.Pending || approval.CurrentStep < 0 || approval.CurrentStep >= steps.Count)
            {
                yield break;
            }

            var step = steps[approval.CurrentStep];
            var decided = decisions.Where(x => x.Step == approval.CurrentStep).Select(x => x.IdentityId).ToHashSet();

            foreach (var identityId in (step.Identities ?? []).Where(x => !decided.Contains(x)).Distinct())
            {
                if (Person($"object-property-approver-open-{identityId:N}", identityId, step.Name) is { } row)
                {
                    row.Status = "kleenestar.core:object.property.people.pending";
                    row.StatusTone = "pending";

                    yield return row;
                }
            }

            foreach (var groupId in (step.Groups ?? []).Distinct())
            {
                var group = CoreHub.GroupManager.GetGroup(groupId);

                if (group is null)
                {
                    continue;
                }

                yield return new ObjectPersonRow($"object-property-approver-group-{groupId:N}")
                {
                    Name = group.Name,
                    Caption = step.Name,
                    Group = true,
                    Status = "kleenestar.core:object.property.people.pending",
                    StatusTone = "pending"
                };
            }
        }

        /// <summary>
        /// Creates the row of an account, or <see langword="null"/> when the id names none.
        /// </summary>
        /// <param name="id">The id of the row.</param>
        /// <param name="identityId">The account.</param>
        /// <param name="role">The role line.</param>
        /// <returns>The row, or <see langword="null"/>.</returns>
        private static ObjectPersonRow Person(string id, Guid? identityId, string role)
        {
            var identity = identityId is Guid value && value != Guid.Empty ? CoreHub.IdentityManager.GetIdentity(value) : null;

            if (identity is null || string.IsNullOrWhiteSpace(identity.Name))
            {
                return null;
            }

            return new ObjectPersonRow(id)
            {
                Name = identity.Name,
                Caption = role,
                Image = ObjectBoardProjection.AvatarImage(identity)
            };
        }

        /// <summary>
        /// Creates the small heading above a group of rows.
        /// </summary>
        /// <param name="id">The id of the heading.</param>
        /// <param name="text">The i18n key of the text.</param>
        /// <returns>The heading.</returns>
        private static ControlText Heading(string id, string text)
        {
            return new ControlText(id)
            {
                Text = _ => text,
                Format = _ => TypeFormatText.Small,
                Classes = ["ks-people-heading"]
            };
        }
    }
}
