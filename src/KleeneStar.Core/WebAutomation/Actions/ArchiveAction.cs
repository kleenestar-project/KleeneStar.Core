using KleeneStar.Core.WebManager;
using KleeneStar.Model.Entities;
using System;
using System.Globalization;

namespace KleeneStar.Core.WebAutomation.Actions
{
    /// <summary>
    /// <c>archive [days]</c> - archives the object when it is done and has not changed for at
    /// least that many days; without days, archives it now.
    /// </summary>
    /// <remarks>
    /// The retention rule of a service desk is a scheduled rule with this one line
    /// (<c>archive 90</c>): it runs over every object of its scope and archives what has been
    /// closed long enough, and leaves everything else alone. An archived object leaves the
    /// working lists and stays readable, searchable and restorable; nothing is deleted.
    /// </remarks>
    internal sealed class ArchiveAction : IAutomationAction
    {
        /// <inheritdoc/>
        public string Key => "archive";

        /// <inheritdoc/>
        public string Syntax => "kleenestar.core:automation.action.archive";

        /// <inheritdoc/>
        public string Validate(string argument, Class @class)
        {
            return string.IsNullOrWhiteSpace(argument) || (int.TryParse(argument.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) && days >= 0)
                ? null
                : "give the days as a whole number, or nothing to archive at once.";
        }

        /// <inheritdoc/>
        public void Execute(AutomationActionContext context, string argument)
        {
            var @object = context.Object;
            var days = string.IsNullOrWhiteSpace(argument) ? 0 : int.Parse(argument.Trim(), CultureInfo.InvariantCulture);

            // already archived - by an earlier attempt, or by hand - is the outcome the rule wants
            if (@object.State == WorkspaceState.Archived)
            {
                return;
            }

            if (days > 0 && (!PlanningManager.IsDone(@object) || @object.Updated > DateTime.UtcNow.AddDays(-days)))
            {
                return;
            }

            @object.State = WorkspaceState.Archived;
            CoreHub.ObjectManager.Update(@object);
        }
    }
}
