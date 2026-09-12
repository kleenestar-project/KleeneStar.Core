using System.Linq;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// Puts the input that names a record onto the <c>header</c> of its form, which is what
    /// makes it the title of the dialog the form is opened as.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The header is where a form names what is being edited, and every dialog in the
    /// framework lifts it onto its own title bar - the form therefore says the same thing on
    /// the page and in the dialog without knowing which of the two it is being fetched for,
    /// which it could not know: both are the same request. The section stays <em>inside</em>
    /// the form, so the name is loaded with the row and submitted with the answers like any
    /// other field; only where it is drawn changes.
    /// </para>
    /// <para>
    /// The base form emits a header only when a fragment contributed one, so one is created
    /// when there is none, and it is placed ahead of the body rather than appended - on the
    /// page, where nothing lifts it, the name of the record belongs above it. The input
    /// itself carries the framework's <c>wx-modal-title-input</c> mark, which is what makes
    /// the title bar draw it as a title rather than as a box standing in it; the mark is put
    /// on by the fragment, because it is the fragment that decides the input is the title.
    /// </para>
    /// <para>
    /// The object edit mask and the class edit dialog share this, so the record's own name
    /// titles both the way the prose editor titles a document.
    /// </para>
    /// </remarks>
    public static class FormTitleInput
    {
        /// <summary>
        /// The class the framework reads as <em>this input is the dialog's title</em>.
        /// </summary>
        public const string Mark = "wx-modal-title-input";

        /// <summary>
        /// Renders the input into the header of the rendered form.
        /// </summary>
        /// <param name="node">The rendered form.</param>
        /// <param name="input">The input that names the record. It is initialized and rendered here, so it must not be among the form's items as well.</param>
        /// <param name="renderContext">The context in which the form is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>The form, with the input on its header; the node unchanged when it is not an element.</returns>
        public static IHtmlNode Place(IHtmlNode node, ControlFormItem input, IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            if (node is not IHtmlElement form || input is null)
            {
                return node;
            }

            input.Initialize(renderContext);

            var title = input.Render(renderContext, visualTree);
            var header = form.Elements.OfType<HtmlElementSectionHeader>().FirstOrDefault();

            if (header is not null)
            {
                // the name of the record leads whatever else a fragment put on the header
                var contributed = header.Elements.ToList();
                header.Clear();
                header.Add(title);
                header.Add(contributed);

                return form;
            }

            var children = form.Elements.ToList();
            var main = children.OfType<HtmlElementSectionMain>().FirstOrDefault();

            form.Clear();

            foreach (var child in children)
            {
                if (child == main)
                {
                    form.Add(new HtmlElementSectionHeader(title));
                }

                form.Add(child);
            }

            return form;
        }
    }
}
