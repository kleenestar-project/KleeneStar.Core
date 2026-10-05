using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WebFragment.Insight
{
    /// <summary>
    /// Composes the query field the add, edit and clone dialogs of an insight share: the WQL
    /// prompt of the search page, labelled and explained.
    /// </summary>
    /// <remarks>
    /// The query is what every tab of the insight shows, so it is written in the prompt the
    /// advanced search offers - completion of attribute and value names and the syntax check
    /// against the object model - rather than in a text box. The endpoint refuses a query that
    /// does not compile, with the parser's reason.
    /// </remarks>
    internal static class InsightFormItems
    {
        /// <summary>
        /// The name the query is submitted and loaded under.
        /// </summary>
        public const string QueryField = nameof(Model.Entities.Insight.Query);

        /// <summary>
        /// Builds the WQL prompt of a dialog.
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
                Text = _ => "kleenestar.core:insight.query.label"
            };

            var help = new ControlFormItemHelpText($"{prompt.Id}-help")
            {
                Text = _ => "kleenestar.core:insight.query.help"
            };

            return new ControlFormItemPanel($"{prompt.Id}-panel", label, prompt, help);
        }
    }
}
