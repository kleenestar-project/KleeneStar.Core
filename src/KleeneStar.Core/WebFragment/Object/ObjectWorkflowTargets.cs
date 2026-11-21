using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebUri;

namespace KleeneStar.Core.WebFragment.Object
{
    using Field = KleeneStar.Model.Entities.Field;
    using FormEntity = KleeneStar.Model.Entities.Form;
    using Status = KleeneStar.Model.Entities.Status;
    using Workflow = KleeneStar.Model.Entities.Workflow;

    /// <summary>
    /// The workflow fields of an object and the states it may be moved to, shared by the status
    /// card of the detail page (<see cref="ObjectPropertyWorkflowCardFragment"/>) and the
    /// overflow menu of the preview pane (<see cref="ObjectPreviewWorkflowMoreFragment"/>).
    /// </summary>
    internal static class ObjectWorkflowTargets
    {
        /// <summary>
        /// Returns the workflow fields of the object's class: active, not deprecated, of type
        /// <see cref="FieldType.Workflow"/> and bound to a workflow.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <returns>The fields in declaration order.</returns>
        public static IReadOnlyList<Field> Fields(Model.Entities.Object @object)
        {
            return [.. CoreHub.FieldManager
                .GetFields(new ClassIdParameter(@object.ClassId))
                .Where(f => !f.Deprecated
                    && f.State == FieldState.Active
                    && f.FieldType == FieldType.Workflow
                    && f.WorkflowId.HasValue)];
        }

        /// <summary>
        /// Returns the states the acting identity may move the object to through the supplied
        /// field, each with the active transition from the current state that leads there.
        /// </summary>
        /// <remarks>
        /// <see cref="WebManager.IWorkflowManager.GetOfferedStatuses"/> evaluates the guards for
        /// the identity, so a refused transition is not offered. The transition is looked up for
        /// its label only; the endpoint resolves the state change itself, so a target reachable
        /// through several transitions appears once.
        /// </remarks>
        /// <param name="object">The object.</param>
        /// <param name="field">The workflow field.</param>
        /// <param name="workflow">The workflow, loaded with its structure.</param>
        /// <param name="current">The current state, or <see langword="null"/>.</param>
        /// <returns>The targets.</returns>
        public static IReadOnlyList<(Status Status, Transition Transition)> Offered(Model.Entities.Object @object, Field field, Workflow workflow, Status current)
        {
            return [.. CoreHub.WorkflowManager
                .GetOfferedStatuses(workflow, current, @object, field, CoreHub.SessionManager.GetCurrentIdentityId(null))
                .Select(status => (status, (workflow.Transitions ?? [])
                    .FirstOrDefault(t => t.State == TransitionState.Active
                        && current is not null
                        && t.SourceId == current.Id
                        && t.TargetId == status.Id)))];
        }

        /// <summary>
        /// Returns the address of the transition endpoint that moves the object into the target
        /// state through the supplied field.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="field">The workflow field.</param>
        /// <param name="target">The target state.</param>
        /// <returns>The address.</returns>
        public static IUri TransitionUri(Model.Entities.Object @object, Field field, Status target)
        {
            return CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Transitions._objectkey_.Index>()?
                .BindParameters(new ObjectKeyParameter(@object.Key))
                .Add(new UriQuery(FieldIdParameter.Key, field.Id.ToString()))
                .Add(new UriQuery(WorkflowStateIdParameter.Key, target.Id.ToString()));
        }

        /// <summary>
        /// Returns the address of the dialog that shows the form of a transition before moving
        /// the object into the target state through the supplied field.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="field">The workflow field.</param>
        /// <param name="target">The target state.</param>
        /// <returns>The address.</returns>
        public static IUri ScreenUri(Model.Entities.Object @object, Field field, Status target)
        {
            return CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.Transition>()?
                .BindParameters(new ObjectKeyParameter(@object.Key))
                .Add(new UriQuery(FieldIdParameter.Key, field.Id.ToString()))
                .Add(new UriQuery(WorkflowStateIdParameter.Key, target.Id.ToString()));
        }

        /// <summary>
        /// Returns the form a transition shows before it runs, or <see langword="null"/> when the
        /// move is made at once.
        /// </summary>
        /// <remarks>
        /// An archived form is treated as none. The transition keeps the reference, so restoring
        /// the form brings the dialog back, but a form nobody maintains any more is not put in
        /// front of a person moving a record.
        /// </remarks>
        /// <param name="transition">The transition, or <see langword="null"/> for an entry move.</param>
        /// <param name="withStructure">Whether the tabs and elements of the form are loaded.</param>
        /// <returns>The form, or <see langword="null"/>.</returns>
        public static FormEntity ScreenForm(Transition transition, bool withStructure = false)
        {
            if (transition?.ScreenFormId is not { } formId)
            {
                return null;
            }

            var form = withStructure
                ? CoreHub.FormManager.GetFormWithStructure(formId)
                : CoreHub.FormManager.GetForm(formId);

            return form is { State: FormState.Active } ? form : null;
        }
    }
}
