using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WebManager
{
    // The entity types Object/Field/Status collide with System.Object and the
    // KleeneStar.Core.WWW.* namespace segments of the same name; alias them inside the
    // namespace block (see the Calendar namespace-collision note).
    using Field = KleeneStar.Model.Entities.Field;
    using ObjectEntity = KleeneStar.Model.Entities.Object;
    using Status = KleeneStar.Model.Entities.Status;

    /// <summary>
    /// Derives the running clock of a single <see cref="SlaTarget"/> on a single object as
    /// the <see cref="SlaDefinition"/> that <see cref="SlaEvaluator"/>, the
    /// <c>ControlDataSla</c> widget and its REST endpoint all evaluate.
    /// </summary>
    /// <remarks>
    /// The clock is derived rather than stored: <b>KleeneStar</b> persists policies, not
    /// per-object timers. The reading is therefore
    /// <list type="bullet">
    /// <item><description>the clock starts when the object was created,</description></item>
    /// <item><description>it is stopped while the object sits in one of the policy's
    /// <see cref="SlaPolicy.PauseOn"/> statuses, and</description></item>
    /// <item><description>it is settled once the object reaches a status of the
    /// <c>done</c> category.</description></item>
    /// </list>
    /// Both the stop and the settlement are dated at <see cref="ObjectEntity.Updated"/>,
    /// because the workflow transition that put the object into that status is what stamped
    /// it. Two consequences follow and are deliberate: pause time accrued <i>before</i> the
    /// current status cannot be reconstructed (there is no status history to read it from),
    /// so <see cref="SlaDefinition.PauseTotal"/> stays zero; and an unrelated edit while the
    /// object is paused moves the stop forward. Both resolve themselves the moment a real
    /// per-object clock is persisted — this type is then the only place that has to change.
    ///
    /// The budget is wall-clock time. The working-hours <see cref="SlaPolicy.CalendarId"/>
    /// is not evaluated here, so a policy bound to a business calendar counts nights and
    /// holidays against its target.
    /// </remarks>
    public static class SlaClock
    {
        /// <summary>
        /// The length of a business day, used to express a
        /// <see cref="SlaTargetUnit.BusinessDays"/> target as wall-clock time.
        /// </summary>
        public const int BusinessHoursPerDay = 8;

        /// <summary>
        /// The status category whose members count as the end of the agreement.
        /// </summary>
        private const string DoneCategory = "done";

        /// <summary>
        /// Returns the time budget of a target, expressed in its unit.
        /// </summary>
        /// <param name="target">The target to measure.</param>
        /// <returns>The budget, or <see cref="TimeSpan.Zero"/> when there is no target.</returns>
        public static TimeSpan GetBudget(SlaTarget target)
        {
            if (target is null || target.TargetValue <= 0)
            {
                return TimeSpan.Zero;
            }

            return target.Unit switch
            {
                SlaTargetUnit.Minutes => TimeSpan.FromMinutes(target.TargetValue),
                SlaTargetUnit.Hours => TimeSpan.FromHours(target.TargetValue),
                SlaTargetUnit.Days => TimeSpan.FromDays(target.TargetValue),
                SlaTargetUnit.BusinessDays => TimeSpan.FromHours(target.TargetValue * (double)BusinessHoursPerDay),
                _ => TimeSpan.Zero
            };
        }

        /// <summary>
        /// Builds the definition of the clock a target runs on a given object.
        /// </summary>
        /// <param name="object">The object the agreement is measured against.</param>
        /// <param name="policy">The policy the target belongs to.</param>
        /// <param name="target">The target being measured.</param>
        /// <param name="status">The workflow status the object currently carries, or
        /// <c>null</c> when it carries none.</param>
        /// <param name="moment">The moment the clock is read at.</param>
        /// <returns>The definition, or <c>null</c> when there is no object or no target.</returns>
        public static SlaDefinition Derive(ObjectEntity @object, SlaPolicy policy, SlaTarget target, Status status, DateTime moment)
        {
            if (@object is null || target is null)
            {
                return null;
            }

            var definition = new SlaDefinition
            {
                Start = @object.Created,
                Target = GetBudget(target),
                Recurrence = TypeRecurrenceSla.None,
                Cycles = 1
            };

            var settled = IsSettled(status);
            var paused = !settled && IsPaused(policy, status);

            if (!settled && !paused)
            {
                return definition;
            }

            // both readings freeze the clock at the transition that caused them, which is
            // the last stamp the object carries
            var stopped = Clamp(@object.Updated, @object.Created, moment);
            definition.PausedSince = stopped;

            if (settled)
            {
                // the agreement does not recur, so the settled cycle is always the first
                definition.FulfilledCycle = 1;
                definition.FulfilledAt = stopped;
            }

            return definition;
        }

        /// <summary>
        /// Returns whether the policy stops its clock in the given status, which it does
        /// when the status is named in the policy's comma-separated
        /// <see cref="SlaPolicy.PauseOn"/> list.
        /// </summary>
        /// <param name="policy">The policy to read the pause statuses from.</param>
        /// <param name="status">The status the object carries, or <c>null</c>.</param>
        /// <returns><c>true</c> when the clock is stopped.</returns>
        public static bool IsPaused(SlaPolicy policy, Status status)
        {
            if (string.IsNullOrWhiteSpace(policy?.PauseOn) || string.IsNullOrWhiteSpace(status?.Name))
            {
                return false;
            }

            return policy.PauseOn
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(x => string.Equals(x, status.Name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns whether the agreement is settled, which it is once the object has reached
        /// a status of the <c>done</c> category. The category rather than the status name is
        /// asked, so a class that renames its closing states keeps its agreements correct.
        /// </summary>
        /// <param name="status">The status the object carries, or <c>null</c>.</param>
        /// <returns><c>true</c> when the agreement is settled.</returns>
        public static bool IsSettled(Status status)
        {
            return Normalize(status?.Category?.Name) == DoneCategory;
        }

        /// <summary>
        /// Resolves the workflow status the object currently carries, by reading the value of
        /// the first active workflow-backed field of its class. A class that drives several
        /// workflows has no single lifecycle status, so the first one that resolves decides -
        /// the same reading the object's status badge takes.
        /// </summary>
        /// <param name="object">The object whose status is resolved.</param>
        /// <param name="fieldManager">The field manager used to enumerate the class fields.</param>
        /// <param name="valueManager">The value manager used to read the field value.</param>
        /// <param name="workflowManager">The workflow manager used to resolve the value
        /// against the workflow's states.</param>
        /// <returns>The status, or <c>null</c> when the object carries none.</returns>
        public static Status ResolveStatus
        (
            ObjectEntity @object,
            IFieldManager fieldManager,
            IValueManager valueManager,
            IWorkflowManager workflowManager
        )
        {
            if (@object is null || fieldManager is null || valueManager is null || workflowManager is null)
            {
                return null;
            }

            var fields = fieldManager
                .GetFields(new ClassIdParameter(@object.ClassId))
                .Where(f => !f.Deprecated
                    && f.State == FieldState.Active
                    && f.FieldType == FieldType.Workflow
                    && f.WorkflowId.HasValue);

            foreach (var field in fields)
            {
                var status = ResolveStatus(@object, field, valueManager, workflowManager);

                if (status is not null)
                {
                    return status;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the status a single workflow-backed field carries on the object.
        /// </summary>
        /// <param name="object">The object whose value is read.</param>
        /// <param name="field">The workflow-backed field.</param>
        /// <param name="valueManager">The value manager used to read the field value.</param>
        /// <param name="workflowManager">The workflow manager used to resolve the value.</param>
        /// <returns>The status, or <c>null</c>.</returns>
        private static Status ResolveStatus(ObjectEntity @object, Field field, IValueManager valueManager, IWorkflowManager workflowManager)
        {
            // the states are needed to resolve the payload, so the structural load is used
            // rather than the shallow header read
            var workflow = workflowManager.GetWorkflowWithStructure(field.WorkflowId.Value);

            if (workflow is null)
            {
                return null;
            }

            var value = valueManager.GetValue(@object.Id, field.Id);

            return workflowManager.ResolveStatus(workflow, value?.Data);
        }

        /// <summary>
        /// Reduces a string to its lower-cased alphanumeric characters so a category name can
        /// be compared regardless of how it is spaced or cased.
        /// </summary>
        /// <param name="value">The value to normalize.</param>
        /// <returns>The normalized string.</returns>
        private static string Normalize(string value)
        {
            return new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        /// <summary>
        /// Keeps a moment inside the window the clock can meaningfully report, so a stamp
        /// that predates the start or lies in the future cannot hand the agreement extra
        /// budget or a negative elapsed time.
        /// </summary>
        /// <param name="value">The moment to clamp.</param>
        /// <param name="min">The earliest permitted moment.</param>
        /// <param name="max">The latest permitted moment.</param>
        /// <returns>The clamped moment.</returns>
        private static DateTime Clamp(DateTime value, DateTime min, DateTime max)
        {
            if (max < min)
            {
                return min;
            }

            return value < min ? min : value > max ? max : value;
        }
    }
}
