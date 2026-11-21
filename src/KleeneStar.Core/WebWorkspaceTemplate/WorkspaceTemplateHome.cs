using System.Collections.Generic;

namespace KleeneStar.Core.WebWorkspaceTemplate
{
    /// <summary>
    /// What the home page of a workspace created from a template says: a lead paragraph and
    /// the sections below it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every text is an internationalization key or free text; free text passes the translator
    /// unchanged. The texts are resolved once, when the page is created, in the culture of the
    /// creating request (<see cref="WorkspaceTemplateContent"/>). The stored page contains the
    /// resolved text, not the declaration.
    /// </para>
    /// <para>
    /// Banner, class list and footer are rendered by the core on every home page; the template
    /// declares only the workspace specific content.
    /// </para>
    /// </remarks>
    public sealed class WorkspaceTemplateHome
    {
        /// <summary>
        /// Gets the paragraph stating the purpose of the workspace. It follows the workspace
        /// description when one exists.
        /// </summary>
        public string Lead { get; init; }

        /// <summary>
        /// Gets the sections of the page, in reading order.
        /// </summary>
        public IReadOnlyList<WorkspaceTemplateHomeSection> Sections { get; init; } = [];
    }

    /// <summary>
    /// One section of a template's home page: a heading, an optional paragraph and an
    /// optional list.
    /// </summary>
    public sealed class WorkspaceTemplateHomeSection
    {
        /// <summary>
        /// Gets the heading.
        /// </summary>
        public string Heading { get; init; }

        /// <summary>
        /// Gets the paragraph under the heading, or <see langword="null"/>.
        /// </summary>
        public string Text { get; init; }

        /// <summary>
        /// Gets the entries of the list under the paragraph. Empty for none.
        /// </summary>
        public IReadOnlyList<string> Items { get; init; } = [];

        /// <summary>
        /// Gets whether the list is a sequence of steps (numbered) rather than a set of points.
        /// </summary>
        public bool Numbered { get; init; }

        /// <summary>
        /// Returns every text of the section: heading, paragraph and entries.
        /// </summary>
        /// <returns>The texts, without the empty ones.</returns>
        public IEnumerable<string> Texts()
        {
            if (!string.IsNullOrWhiteSpace(Heading))
            {
                yield return Heading;
            }

            if (!string.IsNullOrWhiteSpace(Text))
            {
                yield return Text;
            }

            foreach (var item in Items ?? [])
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    yield return item;
                }
            }
        }
    }
}
