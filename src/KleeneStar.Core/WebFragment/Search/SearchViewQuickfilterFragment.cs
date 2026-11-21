using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;
using WebExpress.WebUI.WebSection;

// the fragment exposes a Quickfilter property of its own, so the endpoint of the same name is
// reached through an alias rather than through a qualified name inside every reference
using SavedSearchQuickfilterApi = KleeneStar.Core.WWW.Api._1_.SavedSearch._savedsearchid_.Quickfilter;

namespace KleeneStar.Core.WebFragment.Search
{
    /// <summary>
    /// The quickfilter bar of the saved search the search page runs: the filters defined for it,
    /// and the chip that defines another.
    /// </summary>
    /// <remarks>
    /// The bar belongs to a saved search, not to the page - an ad-hoc search has no place to keep
    /// a filter, so without a saved search (or for a caller who may not see it) nothing renders.
    /// It is served under the saved search's route, and the filter dialogs name it as the context
    /// of the view <see cref="SavedSearchQuickfilterApi.ViewKey"/>. The results table applies the
    /// active chips through its filter binding.
    /// </remarks>
    [Section<SectionViewHeaderSecondary>]
    [Scope<SearchViewFragment>]
    [Cache]
    public sealed class SearchViewQuickfilterFragment : FragmentControlViewHeader
    {
        /// <summary>
        /// Represents the unique identifier for the content.
        /// </summary>
        public static readonly string ContentId = "id_7A1C5E3B9D2F4B6A8C0E1F2A3B4C5D6E";

        /// <summary>
        /// Gets the quickfilter control over the results of the saved search.
        /// </summary>
        public ControlDataQuickfilter Quickfilter { get; } = new ControlDataQuickfilter(ContentId)
        {
            ServiceFactory = renderContext => DataServiceDescriptor.QueryData(ServiceUri(renderContext)),

            // the chips a user defined offer this from their own menu; the bar appends the filter
            // they stand for, so one dialog serves them all
            EditAction = renderContext => new ActionModal
            (
                "modal-form",
                DialogUri<global::KleeneStar.Core.WWW.Quickfilters.Edit>(renderContext),
                TypeModalSize.Large
            )
        };

        /// <summary>
        /// Gets the chip that opens the dialog in which a new quickfilter is defined.
        /// </summary>
        public ControlQuickfilterItemAdd AddFilter { get; } = new()
        {
            Tooltip = _ => "kleenestar.core:quickfilter.add.label",
            PrimaryAction = renderContext => new ActionModal
            (
                "modal-form",
                DialogUri<global::KleeneStar.Core.WWW.Quickfilters.Add>(renderContext),
                TypeModalSize.Large
            )
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public SearchViewQuickfilterFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Quickfilter.Add(AddFilter);

            Add(Quickfilter);
        }

        /// <summary>
        /// Renders the bar while a saved search the caller may see runs, and nothing otherwise.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control, or null.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return SavedSearchRun.Resolve(renderContext?.Request) is null
                ? null
                : base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Returns the address of the bar of the saved search that runs.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The address, or null without a saved search.</returns>
        private static string ServiceUri(IRenderControlContext renderContext)
        {
            var savedSearch = SavedSearchRun.Resolve(renderContext?.Request);

            return savedSearch is null
                ? null
                : CoreHub.GetUri<SavedSearchQuickfilterApi>()?
                    .BindParameters(new SavedSearchIdParameter(savedSearch.Id))?
                    .ToString();
        }

        /// <summary>
        /// Returns the address of a filter dialog, naming the view and the saved search the
        /// filter belongs to.
        /// </summary>
        /// <typeparam name="TPage">The dialog page.</typeparam>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The dialog address.</returns>
        private static IUri DialogUri<TPage>(IRenderControlContext renderContext)
            where TPage : IEndpoint
        {
            var savedSearch = SavedSearchRun.Resolve(renderContext?.Request);

            // a fresh address on every call, so the query added here accumulates nowhere
            return CoreHub.GetUri<TPage>()?
                .Add(new UriQuery("view", SavedSearchQuickfilterApi.ViewKey))
                .Add(new UriQuery("context", savedSearch?.Id.ToString()));
        }
    }
}
