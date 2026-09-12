using KleeneStar.Core.WebControl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.Test.WebControl
{
    /// <summary>
    /// Tests the placement of a record's name on the header of its form - the section a
    /// dialog lifts onto its title bar. The object mask and the class dialog share it, so a
    /// fault here would title both of them wrongly.
    /// </summary>
    public class UnitTestFormTitleInput
    {
        /// <summary>
        /// Builds the shape the data form renders: islands, the main section, the buttons.
        /// </summary>
        /// <param name="header">An optional header a fragment contributed.</param>
        /// <returns>The form.</returns>
        private static HtmlElementFormForm Form(HtmlElementSectionHeader header = null)
        {
            var form = new HtmlElementFormForm();

            form.Add(new HtmlElementTextContentDiv() { Class = "island" });

            if (header is not null)
            {
                form.Add(header);
            }

            form.Add(new HtmlElementSectionMain(new HtmlElementTextContentDiv() { Class = "mask" }));
            form.Add(new HtmlElementTextContentDiv() { Class = "buttons" });

            return form;
        }

        /// <summary>
        /// Builds the form context a control is initialized and rendered against. The input
        /// reads its value off the request, so a request is needed even though it carries
        /// nothing; the page and the endpoint are not.
        /// </summary>
        /// <returns>The form context.</returns>
        private static IRenderControlFormContext CreateFormContext()
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = "GET",
                Protocol = "HTTP/1.1",
                Scheme = "http",
                RawTarget = "/kleenestar/class/1/edit",
                QueryString = string.Empty,
                Headers = new HeaderDictionary { ["Host"] = "localhost", ["Accept-Language"] = "en" }
            });
            features.Set<IHttpConnectionFeature>(new HttpConnectionFeature
            {
                LocalIpAddress = System.Net.IPAddress.Loopback,
                RemoteIpAddress = System.Net.IPAddress.Loopback
            });
            features.Set<IHttpRequestIdentifierFeature>(new HttpRequestIdentifierFeature
            {
                TraceIdentifier = nameof(UnitTestFormTitleInput)
            });

            var context = new WebExpress.WebCore.WebMessage.HttpContext(features, null!);

            return new RenderControlFormContext(null, null, context.Request as Request, null);
        }

        /// <summary>
        /// Renders the form and returns the tag names of its direct children, so the order
        /// can be asserted without parsing markup.
        /// </summary>
        /// <param name="form">The form.</param>
        /// <returns>The child element names in order.</returns>
        private static List<string> Children(IHtmlElement form)
        {
            return form.Elements
                .Select(x => x.ToString().TrimStart())
                .Select(x => x.StartsWith('<') ? new string(x.Skip(1).TakeWhile(char.IsLetter).ToArray()) : x)
                .ToList();
        }

        /// <summary>
        /// A form without a header gets one, placed right before the main section: on the page,
        /// where nothing lifts it, the name belongs above the mask and after the islands.
        /// </summary>
        [Fact]
        public void Place_CreatesTheHeaderAheadOfTheMain()
        {
            var form = Form();
            var input = new ControlFormItemInputText("title") { Name = _ => "Summary", Classes = [FormTitleInput.Mark] };

            var result = FormTitleInput.Place(form, input, CreateFormContext(), null);

            Assert.Same(form, result);
            Assert.Equal(["div", "header", "main", "div"], Children(form));

            var header = form.Elements.OfType<HtmlElementSectionHeader>().Single();
            var html = header.ToString();

            Assert.Contains("wx-modal-title-input", html);
            Assert.Contains("name=\"Summary\"", html);
        }

        /// <summary>
        /// A header a fragment contributed is kept and the name leads what it carries: the
        /// record's name is what a title bar shows first.
        /// </summary>
        [Fact]
        public void Place_LeadsAContributedHeader()
        {
            var contributed = new HtmlElementSectionHeader(new HtmlElementTextSemanticsSpan(new HtmlText("state")) { Class = "state" });
            var form = Form(contributed);
            var input = new ControlFormItemInputText("title") { Name = _ => "Name" };

            FormTitleInput.Place(form, input, CreateFormContext(), null);

            var header = form.Elements.OfType<HtmlElementSectionHeader>().Single();

            Assert.Same(contributed, header);
            Assert.Equal(2, header.Elements.Count());
            Assert.Contains("name=\"Name\"", (header.Elements.First() as IHtmlElement)?.ToString());
            Assert.Contains("state", (header.Elements.Last() as IHtmlElement)?.ToString());
        }

        /// <summary>
        /// A node that is not an element, or no input, leaves the form as it is rather than
        /// failing the render.
        /// </summary>
        [Fact]
        public void Place_LeavesOtherNodesAlone()
        {
            var text = new HtmlText("nothing");
            var form = Form();

            Assert.Same(text, FormTitleInput.Place(text, new ControlFormItemInputText("t"), CreateFormContext(), null));
            Assert.Same(form, FormTitleInput.Place(form, null, CreateFormContext(), null));
            Assert.Equal(["div", "main", "div"], Children(form));
        }
    }
}
