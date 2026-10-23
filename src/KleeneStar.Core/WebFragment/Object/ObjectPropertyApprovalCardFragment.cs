using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;
using ApprovalApi = KleeneStar.Core.WWW.Api._1_.Approvals.ApprovalApi;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The approvals of an object: the latest ones with the step they await and who decided
    /// what, and the ways on - deciding for an approver, withdrawing for the requester or an
    /// administrator, requesting under a policy of the class for whoever may change the object.
    /// </summary>
    /// <remarks>
    /// It renders only for an object whose class has an approval policy or that has approvals.
    /// Issues and assets show it among their properties, documents and blogs below their
    /// content (<see cref="ObjectContentApprovalCardFragment"/>).
    /// </remarks>
    [Section<SectionPropertyPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Order(3)]
    [Cache]
    public class ObjectPropertyApprovalCardFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager resolving the URL-bound object key.</param>
        public ObjectPropertyApprovalCardFragment(IFragmentContext fragmentContext, IObjectManager objectManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;

            Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Three);
        }

        /// <summary>
        /// Renders the card, or nothing when approvals do not concern the object.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>An HTML node representing the rendered fragment, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            string Translate(string key) => I18N.Translate(renderContext, key);

            var @object = _objectManager.GetObjectByKey(renderContext?.Request?.GetParameter<ObjectKeyParameter>());

            if (@object is null)
            {
                return null;
            }

            var approvals = CoreHub.ApprovalManager.GetApprovals(@object.Id);
            var policies = CoreHub.ApprovalManager.GetPolicies(@object.WorkspaceId)
                .Where(x => x.ClassId == @object.ClassId && x.State == ApprovalPolicyState.Active)
                .ToList();

            if (approvals.Count == 0 && policies.Count == 0)
            {
                return null;
            }

            var callerId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request);
            var administrator = ContentAuthorization.MayAdministerWorkspace(@object.WorkspaceId, renderContext.Request);

            Clear();

            Add(new ControlText()
            {
                Text = _ => "kleenestar.core:approval.card.label",
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                Format = _ => TypeFormatText.Small
            });

            foreach (var approval in approvals.Take(3))
            {
                var steps = CoreHub.ApprovalManager.StepsOf(approval);
                var state = Translate("kleenestar.core:approval.state." + approval.State.ToString().ToLowerInvariant());
                var color = approval.State switch
                {
                    ApprovalState.Approved => TypeColorText.Success,
                    ApprovalState.Rejected => TypeColorText.Danger,
                    ApprovalState.Pending => TypeColorText.Warning,
                    _ => TypeColorText.Secondary
                };
                var title = $"{approval.PolicyName}: {state}";

                Add(new ControlText() { Text = _ => title, TextColor = _ => new PropertyColorText(color) });

                if (approval.State == ApprovalState.Pending && approval.CurrentStep < steps.Count)
                {
                    var step = steps[approval.CurrentStep];
                    var awaiting = string.Format(Translate("kleenestar.core:approval.card.awaiting"), approval.CurrentStep + 1, steps.Count, step.Name, step.Approvers);

                    Add(new ControlText() { Text = _ => awaiting, Format = _ => TypeFormatText.Small });
                }

                foreach (var decision in approval.Decisions.OrderBy(x => x.Decided))
                {
                    var line = $"{ApprovalApi.NameOf(decision.IdentityId)}: {Translate("kleenestar.core:approval.verdict." + decision.Verdict.ToString().ToLowerInvariant())}{(decision.Reason is null ? string.Empty : " - " + decision.Reason)}";

                    Add(new ControlText() { Text = _ => line, Format = _ => TypeFormatText.Small, TextColor = _ => new PropertyColorText(TypeColorText.Secondary) });
                }

                if (approval.Outcome is not null)
                {
                    var outcome = approval.Outcome;

                    Add(new ControlText() { Text = _ => outcome, Format = _ => TypeFormatText.Small, TextColor = _ => new PropertyColorText(TypeColorText.Danger) });
                }

                if (CoreHub.ApprovalManager.IsApprover(approval, callerId))
                {
                    var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Approvals.Decide>()?.Add(new UriQuery("approval", approval.Id.ToString()));

                    Add(new ControlLink() { Text = _ => "kleenestar.core:approval.card.decide", Uri = _ => uri });
                }

                if (approval.State == ApprovalState.Pending && (administrator || (callerId != System.Guid.Empty && callerId == approval.RequestedById)))
                {
                    var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Approvals.Cancel>()?.Add(new UriQuery("approval", approval.Id.ToString()));

                    Add(new ControlLink() { Text = _ => "kleenestar.core:approval.card.cancel", Uri = _ => uri });
                }
            }

            if (ContentAuthorization.MayWrite(@object, renderContext.Request))
            {
                foreach (var policy in policies.Where(x => !approvals.Any(a => a.PolicyId == x.Id && a.State == ApprovalState.Pending)))
                {
                    var label = string.Format(Translate("kleenestar.core:approval.card.request"), policy.Name);
                    var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Approvals.Request>()?
                        .Add(new UriQuery("object", @object.Key))
                        .Add(new UriQuery("policy", policy.Id.ToString()));

                    Add(new ControlLink() { Text = _ => label, Uri = _ => uri });
                }
            }

            return base.Render(renderContext, visualTree);
        }
    }

    /// <summary>
    /// The approvals of a document or a blog post, below its content - the pages have no
    /// property column.
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Order(90)]
    [Cache]
    public sealed class ObjectContentApprovalCardFragment : ObjectPropertyApprovalCardFragment
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager resolving the URL-bound object key.</param>
        public ObjectContentApprovalCardFragment(IFragmentContext fragmentContext, IObjectManager objectManager)
            : base(fragmentContext, objectManager)
        {
        }
    }
}
