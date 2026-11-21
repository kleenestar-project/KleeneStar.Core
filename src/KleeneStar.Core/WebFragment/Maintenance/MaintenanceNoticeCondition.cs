using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebFragment.Maintenance
{
    /// <summary>
    /// Represents a condition that determines whether the maintenance notice is currently being
    /// made to the users.
    /// </summary>
    /// <remarks>
    /// It gates <see cref="MaintenanceToastFragment"/>, so the toast is absent from every page
    /// while no announcement is active. The question is asked per request rather than once at
    /// startup, because the notice is switched on and off from the settings page during operation.
    /// </remarks>
    internal class MaintenanceNoticeCondition : ICondition
    {
        /// <summary>
        /// Determines whether an instruction text is to be shown.
        /// </summary>
        /// <param name="request">The request the condition is evaluated for.</param>
        /// <returns>True when the notice is enabled and carries a text.</returns>
        public bool Fulfillment(IRequest request)
        {
            return CoreHub.MaintenanceManager?.IsNoticeVisible() ?? false;
        }
    }
}
