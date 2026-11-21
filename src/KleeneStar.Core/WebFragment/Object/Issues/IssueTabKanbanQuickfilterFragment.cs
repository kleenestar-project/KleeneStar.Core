using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;

// the fragment exposes a Quickfilter property of its own, so the endpoint of the same name is
// reached through an alias rather than through a qualified name inside every reference
using KanbanQuickfilterApi = KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_.KanbanQuickfilter;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// Quickfilter header of the issue Kanban tab: the personal scopes the board can honour
    /// (starred, assigned to me, created by me) followed by the filters the user defined,
    /// with the chip that opens the editor for a new one.
    /// </summary>
    /// <remarks>
    /// The filters are stored under the issue list's view key, so a filter defined here
    /// shows on the list too and the other way round. The selection is written into the
    /// shared query state of <see cref="IssueTabKanbanFragment"/>, which re-queries the
    /// board.
    /// </remarks>
    [Section<SectionTabTemplatePrimary>]
    [Scope<IssueTabKanbanTemplateFragment>]
    [Order(1)]
    [Cache]
    public sealed class IssueTabKanbanQuickfilterFragment : FragmentControlPanel
    {
        /// <summary>
        /// Represents the unique identifier for the content used in the application.
        /// </summary>
        public static readonly string ContentId = "id_5C2A8F41B7E349D6A0C3E9B1F4D82753";

        /// <summary>
        /// Gets the quickfilter control offering the board scopes and the user's own filters.
        /// </summary>
        public ControlDataQuickfilter Quickfilter { get; } = new ControlDataQuickfilter(ContentId)
        {
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<KanbanQuickfilterApi>().ToString()),

            // the chips a user defined offer this from their own menu; the bar appends the
            // filter they stand for, so one dialog serves them all
            EditAction = renderContext => new ActionModal
            (
                "modal-form",
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Quickfilters.Edit>()
                    .BindParameters(renderContext.Request)
                    .Concat($"?view={KanbanQuickfilterApi.ViewKey}&context={renderContext.Request?.GetParameter<WorkspaceKeyParameter>()?.Value}"),
                TypeModalSize.Large
            )
        };

        /// <summary>
        /// Gets the chip that opens the dialog in which a new quickfilter is defined.
        /// </summary>
        /// <remarks>
        /// The chip carries no filter and never shows active; the client keeps it at the
        /// trailing edge of the bar.
        /// </remarks>
        public ControlQuickfilterItemAdd AddFilter { get; } = new()
        {
            Tooltip = _ => "kleenestar.core:quickfilter.add.label",
            PrimaryAction = renderContext => new ActionModal
            (
                "modal-form",
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Quickfilters.Add>()
                    .BindParameters(renderContext.Request)
                    .Concat($"?view={KanbanQuickfilterApi.ViewKey}&context={renderContext.Request?.GetParameter<WorkspaceKeyParameter>()?.Value}"),
                TypeModalSize.Large
            )
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabKanbanQuickfilterFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // the cast picks the writing-surface overload of Resource<T> — without it the
            // compiler settles on the ControlDataList one and fails on the receiver type
            ((IViewStateModelBound)Quickfilter).Resource<IssueKanbanResource>().Model("filter");

            Quickfilter.Add(AddFilter);

            Add(Quickfilter);
        }
    }
}
