using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebUI.WebControl;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The state of one service level target on one object.
    /// </summary>
    /// <param name="Policy">The agreement.</param>
    /// <param name="Target">The target.</param>
    /// <param name="Evaluation">How the target stands.</param>
    public sealed record SlaTargetEvaluation(SlaPolicy Policy, SlaTarget Target, SlaEvaluation Evaluation);

    /// <summary>
    /// Evaluates every active service level target that applies to an object - the one answer
    /// the escalation rules and the compliance report both read.
    /// </summary>
    public static class SlaEvaluations
    {
        /// <summary>
        /// Evaluates the targets of an object.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="now">The moment, in UTC.</param>
        /// <returns>One evaluation per applying target.</returns>
        public static IReadOnlyList<SlaTargetEvaluation> For(ObjectEntity @object, DateTime now)
        {
            if (@object is null)
            {
                return [];
            }

            var policies = SlaScope.Select
            (
                CoreHub.SlaManager.GetSlas(@object.ClassId).Where(p => p.State == SlaPolicyState.Active),
                @object,
                CoreHub.FieldManager,
                CoreHub.ValueManager,
                CoreHub.PriorityManager,
                CoreHub.ObjectTagManager
            );

            var status = SlaClock.ResolveStatus(@object, CoreHub.FieldManager, CoreHub.ValueManager, CoreHub.WorkflowManager);
            var local = now.ToLocalTime();
            var evaluations = new List<SlaTargetEvaluation>();

            foreach (var policy in policies)
            {
                foreach (var target in policy.Targets ?? [])
                {
                    evaluations.Add(new SlaTargetEvaluation(policy, target, SlaEvaluator.Evaluate(SlaClock.Derive(@object, policy, target, status, local), local)));
                }
            }

            return evaluations;
        }
    }
}
