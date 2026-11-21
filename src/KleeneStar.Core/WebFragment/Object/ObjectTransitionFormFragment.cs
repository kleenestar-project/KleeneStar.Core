using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    using ObjectEntity = KleeneStar.Model.Entities.Object;
    using Status = KleeneStar.Model.Entities.Status;
    using TransitionApi = KleeneStar.Core.WWW.Api._1_.Objects.Transition;

    /// <summary>
    /// The dialog of a transition that asks for input before it runs: the form the workflow gives
    /// the transition, filled from the object, whose submit moves the object and stores the
    /// values through <see cref="TransitionApi"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The field and the target state are carried along as hidden inputs, because the dialog's
    /// data service is addressed by the object alone and the submit has to say which move it is.
    /// The submit button carries the name and glyph of the target state - the entry the person
    /// picked in the status menu - so they confirm the state they chose rather than a generic save
    /// or the name of a transition they never saw.
    /// </para>
    /// <para>
    /// Workflow fields are left out of the form even when it places one: the state moves through
    /// the transition, and an input showing the old state would only contradict it. A transition
    /// whose form is gone in the meantime still renders - with its description and the button -
    /// so a stale status picker does not end in an empty dialog.
    /// </para>
    /// </remarks>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Transition>]
    [Cache]
    public sealed class ObjectTransitionFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the hidden input carrying the workflow field the transition moves.
        /// </summary>
        public PresetHiddenInput FieldId { get; } = new()
        {
            Name = _ => TransitionApi.FieldKey,
            Value = ctx => ctx?.Request?.GetParameter<FieldIdParameter>()?.Value
        };

        /// <summary>
        /// Gets the hidden input carrying the state the transition moves to.
        /// </summary>
        public PresetHiddenInput StateId { get; } = new()
        {
            Name = _ => TransitionApi.StateKey,
            Value = ctx => ctx?.Request?.GetParameter<WorkflowStateIdParameter>()?.Value
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectTransitionFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<TransitionApi>();

            ItemId = renderContext => ResolveObject(renderContext.Request)?.Id.ToString();

            // the page behind the dialog shows the old state until it is loaded again
            SuccessAction = _ => TypeRestFormSuccess.Reload;

            Submit.Text = ctx => Resolve(ctx?.Request).Target?.Name
                ?? "kleenestar.core:object.transition.screen.submit";
            Submit.Icon = ctx => Resolve(ctx?.Request).Target is { } target
                ? ObjectPropertyWorkflowCardFragment.ResolveIcon(target)
                : new IconFloppyDisk();
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree, BuildItems(renderContext.Request));
        }

        /// <summary>
        /// Builds the items of the dialog: the hidden move, the transition's description and the
        /// inputs of its form.
        /// </summary>
        /// <param name="request">The request the dialog is rendered for.</param>
        /// <returns>The form items.</returns>
        private IEnumerable<IControlFormItem> BuildItems(IRequest request)
        {
            yield return FieldId;
            yield return StateId;

            var @object = ResolveObject(request);
            var transition = Resolve(request).Transition;

            if (@object is null || transition is null)
            {
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(transition.Description))
            {
                yield return new ControlFormItemStaticText("transition-description")
                {
                    Text = _ => transition.Description
                };
            }

            var form = ObjectWorkflowTargets.ScreenForm(transition, withStructure: true);
            var values = ObjectFieldSheet.LoadValues(@object.Id)
                .ToDictionary(x => x.Key, x => x.Value.Data);

            foreach (var item in ObjectFormLayout.BuildItems(form, @object.ClassId, values: values, include: f => f.FieldType != FieldType.Workflow))
            {
                yield return item;
            }
        }

        /// <summary>
        /// Resolves the object the dialog is opened on.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The object, or <see langword="null"/>.</returns>
        private static ObjectEntity ResolveObject(IRequest request)
        {
            return CoreHub.ObjectManager.GetObjectByKey(request?.GetParameter<ObjectKeyParameter>());
        }

        /// <summary>
        /// Resolves the move the dialog is opened for: the requested state of the field's workflow
        /// and the active transition that leads there from the object's current state.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The transition and the target state; the transition is <see langword="null"/>
        /// when the move is no longer one the workflow offers, both are when the request names
        /// no state of the field's workflow.</returns>
        private static (Transition Transition, Status Target) Resolve(IRequest request)
        {
            var @object = ResolveObject(request);

            if (@object is null
                || !Guid.TryParse(request.GetParameter<FieldIdParameter>()?.Value, out var fieldId)
                || !Guid.TryParse(request.GetParameter<WorkflowStateIdParameter>()?.Value, out var stateId))
            {
                return (null, null);
            }

            var field = CoreHub.FieldManager.GetField(fieldId);

            if (field is not { FieldType: FieldType.Workflow, WorkflowId: { } workflowId } || field.ClassId != @object.ClassId)
            {
                return (null, null);
            }

            var workflow = CoreHub.WorkflowManager.GetWorkflowWithStructure(workflowId);
            var target = (workflow?.Statuses ?? []).FirstOrDefault(s => s.Id == stateId);
            var current = CoreHub.WorkflowManager.ResolveStatus(workflow, CoreHub.ValueManager.GetValue(@object.Id, field.Id)?.Data);

            var transition = current is null
                ? null
                : (workflow?.Transitions ?? [])
                    .FirstOrDefault(t => t.State == TransitionState.Active && t.SourceId == current.Id && t.TargetId == stateId);

            return (transition, target);
        }
    }
}
