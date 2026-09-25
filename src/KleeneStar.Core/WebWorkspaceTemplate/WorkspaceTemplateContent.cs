using KleeneStar.Core.WebControl;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WebWorkspaceTemplate
{
    /// <summary>
    /// Writes the prose a workspace is created with: the home page and the post that announces
    /// the workspace.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is separate from <see cref="WebManager.WorkspaceTemplateManager"/> because the two
    /// answer different questions. The manager decides <i>what</i> a new workspace gets and
    /// makes sure it is not given twice; this decides what the two pages <i>say</i>, which is
    /// writing rather than bookkeeping and changes for entirely different reasons.
    /// </para>
    /// <para>
    /// The text is stored, not rendered: what a page says is the object's
    /// <see cref="Model.Entities.Object.Description"/>, and the reading views hand that to the
    /// framework's rich-text control. It is therefore translated <b>once, at creation</b>, in the
    /// language of whoever created the workspace - an author is going to rewrite the page anyway,
    /// and a page that silently changed language under an author who had edited it would be
    /// worse than one written in the wrong one. That language is the request's, not the
    /// installation's default: the person filling in the wizard is reading it in one particular
    /// language, and the page they get should be in that one.
    /// </para>
    /// <para>
    /// The illustration is an inline <c>data:</c> SVG rather than a file: it needs no route, no
    /// directory, and no cleanup when the workspace is deleted, and it survives a database that
    /// is copied to another installation. It is drawn from the product's own mark - the flat
    /// accent field with the white star of <c>Assets/img/kleenestar.svg</c>, in the workspace's
    /// own accent colour, which is the same colour its generated icon carries. It is emitted as
    /// an <c>&lt;img&gt;</c> rather than as an inline <c>&lt;svg&gt;</c> element because the
    /// pages are editable: a contenteditable surface handles an image as one atom, while inline
    /// SVG is a tree the editor would let a caret walk into.
    /// </para>
    /// </remarks>
    internal static partial class WorkspaceTemplateContent
    {
        /// <summary>
        /// Matches the <c>d</c> attribute of the star in the product's mark. The mark is a
        /// coloured square with one glyph on it; only the glyph is wanted here, because the
        /// square is redrawn in the workspace's own colour and at the banner's proportions.
        /// </summary>
        [GeneratedRegex("<path\\b[^>]*\\bd=\"([^\"]+)\"", RegexOptions.IgnoreCase)]
        private static partial Regex GlyphRegex();

        /// <summary>
        /// The suffix of the embedded mark, matched against the manifest resource names with
        /// their directory separators normalized.
        /// </summary>
        private const string MarkResourceSuffix = "img/kleenestar.svg";

        /// <summary>
        /// The path data of the star, read once on first use. Empty when the resource could not
        /// be read, in which case the banner is drawn without the glyph rather than not at all.
        /// </summary>
        private static readonly Lazy<string> Glyph = new(ReadGlyph);

        /// <summary>
        /// Builds the title of the home page.
        /// </summary>
        /// <param name="workspace">The workspace the page belongs to.</param>
        /// <param name="culture">The language the page is written in.</param>
        /// <returns>The title.</returns>
        public static string HomeSummary(Workspace workspace, CultureInfo culture)
        {
            return Translate(culture, "kleenestar.core:workspace.template.home.summary", workspace?.Name);
        }

        /// <summary>
        /// Builds the title of the opening post.
        /// </summary>
        /// <param name="workspace">The workspace the post belongs to.</param>
        /// <param name="culture">The language the post is written in.</param>
        /// <returns>The title.</returns>
        public static string OpeningPostSummary(Workspace workspace, CultureInfo culture)
        {
            return Translate(culture, "kleenestar.core:workspace.template.post.summary", workspace?.Name);
        }

        /// <summary>
        /// Builds the body of the home page: the banner, what the workspace is for, the classes
        /// it holds, and what to do first.
        /// </summary>
        /// <param name="workspace">The workspace the page belongs to.</param>
        /// <param name="classes">The classes the workspace carries.</param>
        /// <param name="culture">The language the page is written in.</param>
        /// <returns>The body, as rich text.</returns>
        public static string HomeBody(Workspace workspace, IReadOnlyList<Class> classes, CultureInfo culture)
        {
            var html = new StringBuilder();

            html.Append(Banner(workspace, Translate(culture, "kleenestar.core:workspace.template.home.banner")));

            // the workspace's own description leads, because it is what somebody actually wrote
            // about this workspace; the generic sentence only stands in when there is none. The
            // editor stores a document, not markup, and this body is markup - appended as it
            // stands, the serialization is what the reader got under the banner
            if (!ProseText.IsEmpty(workspace?.Description))
            {
                html.Append(DescriptionMarkup(workspace.Description));
            }
            else
            {
                html.Append("<p>")
                    .Append(Escape(Translate(culture, "kleenestar.core:workspace.template.home.lead", workspace?.Name)))
                    .Append("</p>");
            }

            html.Append("<h2>")
                .Append(Escape(Translate(culture, "kleenestar.core:workspace.template.home.classes.header")))
                .Append("</h2>");

            if (classes is { Count: > 0 })
            {
                html.Append("<p>")
                    .Append(Escape(Translate(culture, "kleenestar.core:workspace.template.home.classes.intro", classes.Count)))
                    .Append("</p>");

                html.Append(ClassList(classes, culture));
            }
            else
            {
                html.Append("<p>")
                    .Append(Escape(Translate(culture, "kleenestar.core:workspace.template.home.classes.empty")))
                    .Append("</p>");
            }

            html.Append("<h2>")
                .Append(Escape(Translate(culture, "kleenestar.core:workspace.template.home.next.header")))
                .Append("</h2>");

            html.Append("<ol>");

            foreach (var step in new[] { "one", "two", "three", "four" })
            {
                html.Append("<li>")
                    .Append(Escape(Translate(culture, "kleenestar.core:workspace.template.home.next." + step)))
                    .Append("</li>");
            }

            html.Append("</ol>");

            html.Append("<p><em>")
                .Append(Escape(Translate(culture, "kleenestar.core:workspace.template.home.footer")))
                .Append("</em></p>");

            return html.ToString();
        }

        /// <summary>
        /// Builds the body of the opening post: what was created, out of what, and what is
        /// already set up.
        /// </summary>
        /// <param name="workspace">The workspace the post belongs to.</param>
        /// <param name="template">The template the workspace was created from. May be null.</param>
        /// <param name="classes">The classes the workspace carries.</param>
        /// <param name="culture">The language the post is written in.</param>
        /// <returns>The body, as rich text.</returns>
        public static string OpeningPostBody(Workspace workspace, IWorkspaceTemplate template, IReadOnlyList<Class> classes, CultureInfo culture)
        {
            var html = new StringBuilder();

            html.Append(Banner(workspace, Translate(culture, "kleenestar.core:workspace.template.post.banner")));

            // the template is named when there is one and left unmentioned when there is not,
            // rather than announced as "no template": a workspace set up by hand is not a
            // workspace missing something
            var templateName = template is null ? null : I18N.Translate(culture, template.Name);

            html.Append("<p>")
                .Append(Escape(string.IsNullOrWhiteSpace(templateName)
                    ? Translate(culture, "kleenestar.core:workspace.template.post.lead.plain", workspace?.Name)
                    : Translate(culture, "kleenestar.core:workspace.template.post.lead", workspace?.Name, templateName)))
                .Append("</p>");

            if (classes is { Count: > 0 })
            {
                html.Append("<p>")
                    .Append(Escape(Translate(culture, "kleenestar.core:workspace.template.post.classes", classes.Count)))
                    .Append("</p>");

                html.Append(ClassList(classes, culture));
            }

            html.Append("<p>")
                .Append(Escape(Translate(culture, "kleenestar.core:workspace.template.post.views")))
                .Append("</p>");

            html.Append("<p>")
                .Append(Escape(Translate(culture, "kleenestar.core:workspace.template.post.closing")))
                .Append("</p>");

            return html.ToString();
        }

        /// <summary>
        /// Renders the classes as a list, each with what it holds.
        /// </summary>
        /// <remarks>
        /// A class description is an internationalization key when a template wrote it and free
        /// text when a person did, so it is passed through the translator: a key resolves, and
        /// anything that is not a key is answered unchanged.
        /// </remarks>
        /// <param name="classes">The classes to list.</param>
        /// <param name="culture">The language the list is written in.</param>
        /// <returns>The list markup.</returns>
        private static string ClassList(IEnumerable<Class> classes, CultureInfo culture)
        {
            var html = new StringBuilder("<ul>");

            foreach (var @class in classes)
            {
                html.Append("<li><strong>").Append(Escape(@class.Name)).Append("</strong>");

                // a class described in its dialog carries an editor document; the list wants
                // its words
                var description = ProseText.ToPlainText(I18N.Translate(culture, @class.Description));

                if (!string.IsNullOrWhiteSpace(description))
                {
                    html.Append(" — ").Append(Escape(description));
                }

                html.Append("</li>");
            }

            return html.Append("</ul>").ToString();
        }

        /// <summary>
        /// Draws the banner of a workspace: the product's star on the workspace's own accent
        /// colour, with the workspace key set beside it.
        /// </summary>
        /// <param name="workspace">The workspace the banner stands for.</param>
        /// <param name="caption">The line set under the key.</param>
        /// <returns>The banner as an image paragraph.</returns>
        private static string Banner(Workspace workspace, string caption)
        {
            var accent = CoreHub.AccentColor(workspace?.Id ?? Guid.Empty);
            var glyph = Glyph.Value;
            var key = workspace?.Key ?? string.Empty;

            var svg = new StringBuilder();

            svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1200 300\" width=\"1200\" height=\"300\">");
            svg.Append("<rect width=\"1200\" height=\"300\" fill=\"").Append(accent).Append("\"/>");

            if (!string.IsNullOrEmpty(glyph))
            {
                // the same star twice: once oversized and barely there, bleeding off the right
                // edge the way the product's own headers carry it, and once at reading size as
                // the mark itself
                svg.Append("<path transform=\"translate(890,-72) scale(2.6)\" fill=\"#ffffff\" fill-opacity=\"0.13\" d=\"")
                    .Append(glyph)
                    .Append("\"/>");

                svg.Append("<path transform=\"translate(72,86) scale(0.85)\" fill=\"#ffffff\" d=\"")
                    .Append(glyph)
                    .Append("\"/>");
            }

            svg.Append("<text x=\"248\" y=\"146\" fill=\"#ffffff\" font-size=\"62\" font-weight=\"600\" ")
                .Append("font-family=\"Segoe UI, system-ui, -apple-system, Helvetica, Arial, sans-serif\">")
                .Append(Escape(key))
                .Append("</text>");

            svg.Append("<text x=\"250\" y=\"196\" fill=\"#ffffff\" fill-opacity=\"0.85\" font-size=\"26\" ")
                .Append("font-family=\"Segoe UI, system-ui, -apple-system, Helvetica, Arial, sans-serif\">")
                .Append(Escape(caption))
                .Append("</text>");

            svg.Append("</svg>");

            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg.ToString()));

            return "<p><img src=\"data:image/svg+xml;base64," + encoded + "\" alt=\"" + Escape(caption) + "\"></p>";
        }

        /// <summary>
        /// Reads the path data of the star out of the product's embedded mark.
        /// </summary>
        /// <returns>The path data, or an empty string when the resource could not be read.</returns>
        private static string ReadGlyph()
        {
            var assembly = typeof(WorkspaceTemplateContent).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.Replace('\\', '/')
                    .EndsWith(MarkResourceSuffix, StringComparison.OrdinalIgnoreCase));

            if (resourceName is null)
            {
                return string.Empty;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream is null)
            {
                return string.Empty;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            var match = GlyphRegex().Match(reader.ReadToEnd());

            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        /// <summary>
        /// Translates a message.
        /// </summary>
        /// <param name="culture">The language to write in. Null falls back to the installation's
        /// own, which is what a caller with no request behind it has.</param>
        /// <param name="key">The internationalization key.</param>
        /// <param name="args">The arguments of the message.</param>
        /// <returns>The translated message.</returns>
        private static string Translate(CultureInfo culture, string key, params object[] args)
        {
            if (culture is null)
            {
                return args is null || args.Length == 0 ? I18N.Translate(key) : I18N.Translate(key, args);
            }

            return args is null || args.Length == 0
                ? I18N.Translate(culture, key)
                : I18N.Translate(culture, key, args);
        }

        /// <summary>
        /// Turns a stored description into markup that can stand inside the body.
        /// </summary>
        /// <remarks>
        /// A description written in the prose editor is its versioned document, rendered here
        /// by the framework's own reader (<see cref="ProseText.ToHtml"/>). A value that is no
        /// document is either markup an older editor stored, taken as it stands, or a sentence
        /// a seeder or a script wrote, which is escaped into a paragraph.
        /// </remarks>
        /// <param name="value">The stored description. Must not be empty.</param>
        /// <returns>The markup.</returns>
        private static string DescriptionMarkup(string value)
        {
            if (EditorState.IsState(value))
            {
                return ProseText.ToHtml(value);
            }

            return value.TrimStart().StartsWith('<')
                ? value
                : "<p>" + Escape(value) + "</p>";
        }

        /// <summary>
        /// Escapes text for use inside markup.
        /// </summary>
        /// <remarks>
        /// The values put into these pages are a workspace name, a key and class descriptions -
        /// all of them typed by somebody. They are escaped rather than trusted, because the
        /// result is stored and later handed to a rich-text renderer, which is exactly where an
        /// unescaped angle bracket stops being a typo.
        /// </remarks>
        /// <param name="value">The text to escape.</param>
        /// <returns>The escaped text.</returns>
        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("&", "&amp;", StringComparison.Ordinal)
                    .Replace("<", "&lt;", StringComparison.Ordinal)
                    .Replace(">", "&gt;", StringComparison.Ordinal)
                    .Replace("\"", "&quot;", StringComparison.Ordinal);
        }
    }
}
