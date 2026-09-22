using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// Reads a description as a line of text, and answers whether it says anything at all,
    /// whichever way it was written.
    /// </summary>
    /// <remarks>
    /// Every entity dialog edits its description with the prose editor, which stores its
    /// versioned json document rather than text - so a description written through a dialog
    /// is <c>{"version":1,"doc":…}</c>, while a seeded one is the sentence itself. A card, a
    /// table cell or a tooltip wants the words either way, and printing the document as it is
    /// stored shows the reader the serialization instead. The document is rendered by the
    /// framework's own reader (<see cref="EditorState.ToHtml"/>), so what is stripped to text
    /// here is the same markup a reading view would show, and text that is not a document
    /// passes through untouched.
    /// </remarks>
    public static partial class ProseText
    {
        /// <summary>
        /// Returns the plain text of a description.
        /// </summary>
        /// <param name="value">The stored description: an editor document or text.</param>
        /// <returns>
        /// The words of the document with blocks separated by a single space, the text itself
        /// when it is no document, or the value as given when it is blank or does not parse.
        /// </returns>
        public static string ToPlainText(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !EditorState.IsState(value))
            {
                return value;
            }

            try
            {
                var markup = EditorState.ToHtml(value);
                var text = WebUtility.HtmlDecode(Tags().Replace(markup, " "));

                return Whitespace().Replace(text, " ").Trim();
            }
            catch (JsonException)
            {
                // a string that starts with a brace and is no document is shown as it is
                return value;
            }
        }

        /// <summary>
        /// Returns the markup of a description.
        /// </summary>
        /// <remarks>
        /// A reader that wants more than the words needs the document rendered rather than
        /// stripped - the pictures a blog post illustrates itself with are found in the markup,
        /// and a reader looking for <c>&lt;img&gt;</c> in the stored json finds a post that
        /// shows nothing. A description that is no document is answered as it stands, which is
        /// what a seeded sentence and a legacy html body already are.
        /// </remarks>
        /// <param name="value">The stored description: an editor document or text.</param>
        /// <returns>
        /// The rendered document, or the value as given when it is no document, is blank, or
        /// does not parse.
        /// </returns>
        public static string ToHtml(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !EditorState.IsState(value))
            {
                return value;
            }

            try
            {
                return EditorState.ToHtml(value);
            }
            catch (JsonException)
            {
                return value;
            }
        }

        /// <summary>
        /// Decides whether a description says nothing.
        /// </summary>
        /// <remarks>
        /// A prose editor never submits an empty string: a surface nobody wrote into is still a
        /// document, <c>{"version":1,"doc":{…}}</c> around one empty paragraph, and a caller
        /// comparing it against the empty string reads it as something the user said. The
        /// document is measured with the framework's own reader
        /// (<see cref="EditorState.ValidationText"/>), which counts a picture, an atom and a
        /// rule as one position each - a post that is a photograph and no sentence carries
        /// something, and only a document with neither words nor content is empty.
        /// </remarks>
        /// <param name="value">The stored description: an editor document or text.</param>
        /// <returns>
        /// True when the value is null, blank, or a document nobody put anything into.
        /// </returns>
        public static bool IsEmpty(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (!EditorState.IsState(value))
            {
                return false;
            }

            try
            {
                return string.IsNullOrWhiteSpace(EditorState.ValidationText(value));
            }
            catch (JsonException)
            {
                // a string that starts with a brace and is no document says whatever it says
                return false;
            }
        }

        [GeneratedRegex("<[^>]*>")]
        private static partial Regex Tags();

        [GeneratedRegex(@"\s+")]
        private static partial Regex Whitespace();
    }
}
