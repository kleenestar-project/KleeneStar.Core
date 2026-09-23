namespace KleeneStar.Core.WebWorkspaceTemplate
{
    /// <summary>
    /// One priority of the scale a <see cref="WorkspaceTemplateClass"/> is created with. The
    /// scale is ordered the way the template declares it, most pressing first.
    /// </summary>
    public sealed class WorkspaceTemplatePriority
    {
        /// <summary>
        /// Gets the name of the priority, e.g. <c>P1 - Critical</c> - an internationalization key
        /// or plain text, resolved once when the workspace is created. A priority field stores
        /// the resolved name, so it is data from then on.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Gets what the priority means - an internationalization key or plain text.
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        /// Gets the path of the icon the priority is created with.
        /// </summary>
        public string Icon { get; init; }
    }
}
