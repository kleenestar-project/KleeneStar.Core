using System;
using WebExpress.WebCore.WebIcon;

namespace KleeneStar.Core.WebInsight
{
    /// <summary>
    /// Describes a type of insight tab - one way of showing the objects an insight selects,
    /// such as a table, a dashboard, a Kanban board or the reports. The core registers its
    /// types; a plugin contributes another by registering its own descriptor with
    /// <see cref="InsightViewTypeCatalog.Register"/>, together with the tab template that draws it.
    /// </summary>
    /// <remarks>
    /// The descriptor is the semantic side of the persisted
    /// <see cref="Model.Entities.InsightView.ViewType"/> key. What a tab of the type shows is the
    /// business of its <see cref="Template"/>: a <c>FragmentControlDataTabTemplate</c> scoped to
    /// the insight's tab control (<c>InsightTabFragment</c>), whose content fragments are scoped
    /// to the template in turn - the same way the issue overview composes its tabs. The template
    /// picker of the tab control offers every template scoped to it, so a plugin's template
    /// appears there by being declared; the descriptor is what lets the tab endpoint map a stored
    /// tab to its template and back.
    /// </remarks>
    public interface IInsightViewType
    {
        /// <summary>
        /// Gets the key the type is persisted under, for example <c>reports</c>. Keys are
        /// compared without case.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the internationalization key of the name the type is offered under, which is
        /// also the name a new tab of the type is given.
        /// </summary>
        string Label { get; }

        /// <summary>
        /// Gets the internationalization key of the sentence that explains the type.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the icon the type is listed under, on its tab and in the template picker.
        /// </summary>
        IIcon Icon { get; }

        /// <summary>
        /// Gets the position of the type among the others; lower comes first.
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Gets the type of the tab template fragment that draws a tab of this type.
        /// </summary>
        Type Template { get; }
    }
}
