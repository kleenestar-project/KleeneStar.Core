using KleeneStar.Core.WebFragment.Search;
using System;
using System.Linq;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.SavedSearch
{
    /// <summary>
    /// Composes the fields the add and the edit dialog of a saved search share: the query as
    /// the WQL prompt of the search page, and the description in the prose editor.
    /// </summary>
    /// <remarks>
    /// The query is written in the same prompt the advanced search offers - highlighting,
    /// completion of attribute and value names and the syntax check against the object model
    /// the search runs over - rather than in a text box, so what is stored is what the search
    /// page would accept. The prompt is a form field of its own: named after the property the
    /// endpoint binds, it carries its text into the form data and takes a loaded one back.
    /// </remarks>
    internal static class SavedSearchFormItems
    {
        /// <summary>
        /// The name the query is submitted and loaded under.
        /// </summary>
        public const string QueryField = nameof(Model.Entities.SavedSearch.Query);

        /// <summary>
        /// Builds the WQL prompt of a dialog, pointed at the object WQL endpoint the search page
        /// runs against.
        /// </summary>
        /// <param name="id">The element id of the prompt, distinct per dialog.</param>
        /// <returns>The prompt.</returns>
        public static ControlDataWqlPrompt BuildQuery(string id)
        {
            return new ControlDataWqlPrompt(id)
            {
                Name = _ => QueryField,
                ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Objects.Wql>()?.ToString())
            };
        }

        /// <summary>
        /// Builds the form item holding the label, the prompt and the help text.
        /// </summary>
        /// <param name="prompt">The WQL prompt of the dialog.</param>
        /// <returns>The composed form item.</returns>
        public static ControlFormItemPanel BuildQueryPanel(ControlDataWqlPrompt prompt)
        {
            var label = new ControlFormItemLabel($"{prompt.Id}-label")
            {
                Text = _ => "kleenestar.core:search.saved.query.label"
            };

            var help = new ControlFormItemHelpText($"{prompt.Id}-help")
            {
                Text = _ => "kleenestar.core:search.saved.query.help"
            };

            return new ControlFormItemPanel($"{prompt.Id}-panel", label, prompt, help);
        }

        /// <summary>
        /// Builds the description field, edited in the prose editor like every other entity
        /// description.
        /// </summary>
        /// <returns>The description field.</returns>
        public static ControlFormItemInputText BuildDescription()
        {
            return new ControlFormItemInputText
            {
                Name = _ => nameof(Model.Entities.SavedSearch.Description),
                Label = _ => "kleenestar.core:search.saved.description.label",
                Placeholder = _ => "kleenestar.core:search.saved.description.placeholder",
                Format = _ => TypeEditTextFormat.Wysiwyg,
                Required = _ => false
            };
        }

        /// <summary>
        /// Returns the address of the saved-search endpoint a dialog loads and submits through,
        /// carrying what the search page had on screen when the dialog was opened from there:
        /// the expression, which the endpoint answers as the query to show, and the saved search
        /// that ran, whose column layout a new saved search takes over.
        /// </summary>
        /// <param name="renderContext">The render context of the dialog.</param>
        /// <returns>The endpoint address.</returns>
        public static string ResolveService(IRenderControlContext renderContext)
        {
            var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.SavedSearches.Index>()?.ToString();
            var request = renderContext?.Request;
            var query = new[]
                {
                    (SavedSearchRun.WqlParameter, request?.GetParameter(SavedSearchRun.WqlParameter)?.Value),
                    (SavedSearchRun.Parameter, request?.GetParameter(SavedSearchRun.Parameter)?.Value)
                }
                .Where(x => !string.IsNullOrWhiteSpace(x.Item2))
                .Select(x => $"{x.Item1}={Uri.EscapeDataString(x.Item2)}")
                .ToList();

            return query.Count == 0 || uri is null
                ? uri
                : $"{uri}?{string.Join("&", query)}";
        }
    }
}
