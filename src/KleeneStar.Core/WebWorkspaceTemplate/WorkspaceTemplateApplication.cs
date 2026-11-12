using System;
using System.Globalization;

namespace KleeneStar.Core.WebWorkspaceTemplate
{
    /// <summary>
    /// A template that was just applied to a workspace, announced by
    /// <see cref="WebManager.IWorkspaceTemplateManager.TemplateApplied"/> once its structure,
    /// pages and approval policies exist - so a plugin sets up what the core does not know
    /// (the automation rules of <c>KleeneStar.Templates</c>, for one) on what is there.
    /// </summary>
    /// <param name="Template">The template.</param>
    /// <param name="WorkspaceId">The workspace.</param>
    /// <param name="IdentityId">The creator, or <see cref="Guid.Empty"/>.</param>
    /// <param name="Culture">The language the texts are resolved in, or <see langword="null"/>.</param>
    /// <param name="Processes">The processes set up so far; a listener adds what it skipped.</param>
    public sealed record WorkspaceTemplateApplication(IWorkspaceTemplate Template, Guid WorkspaceId, Guid IdentityId, CultureInfo Culture, WorkspaceTemplateProcessResult Processes);
}
