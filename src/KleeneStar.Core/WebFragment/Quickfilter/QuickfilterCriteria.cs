using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WebFragment.Quickfilter
{
    /// <summary>
    /// Composes the filter expression field of the quickfilter dialogs: the WQL prompt the
    /// user writes in, with the label and the help text around it.
    /// </summary>
    /// <remarks>
    /// The prompt is a form field of its own — it is given the name the endpoint reads the
    /// expression under, and it carries its text into the form data and takes a loaded one
    /// back, the same way a text input does.
    ///
    /// Add and edit build the same field, which is why it is built here rather than twice.
    /// </remarks>
    internal static class QuickfilterCriteria
    {
        /// <summary>
        /// The name the expression is submitted and loaded under.
        /// </summary>
        public const string FieldName = "criteria";

        /// <summary>
        /// The element id of the WQL prompt.
        /// </summary>
        public const string PromptId = "quickfilter-criteria";

        /// <summary>
        /// Builds the WQL prompt of the dialog: named so the form collects it, and pointed
        /// at the WQL endpoint of the entity the addressed bar filters so its completion,
        /// its syntax check and its history speak about the right attributes.
        /// </summary>
        /// <returns>The prompt.</returns>
        public static ControlDataWqlPrompt BuildPrompt()
        {
            return new ControlDataWqlPrompt(PromptId)
            {
                Name = _ => FieldName,
                ServiceFactory = renderContext =>
                {
                    var uri = QuickfilterService.ResolveWql(renderContext);

                    // without an endpoint the prompt stays a syntax-highlighting editor
                    // rather than failing; a bar whose entity has no WQL endpoint is still
                    // usable, only without completion
                    return string.IsNullOrEmpty(uri) ? null : DataServiceDescriptor.QueryData(uri);
                }
            };
        }

        /// <summary>
        /// Builds the form item holding the label, the prompt and the help text.
        /// </summary>
        /// <param name="prompt">The WQL prompt of the dialog.</param>
        /// <returns>The composed form item.</returns>
        public static ControlFormItemPanel BuildPanel(ControlDataWqlPrompt prompt)
        {
            var label = new ControlFormItemLabel($"{PromptId}-label")
            {
                Text = _ => "kleenestar.core:quickfilter.query.label"
            };

            var help = new ControlFormItemHelpText($"{PromptId}-help")
            {
                Text = _ => "kleenestar.core:quickfilter.query.help"
            };

            return new ControlFormItemPanel($"{PromptId}-panel", label, prompt, help);
        }
    }
}
