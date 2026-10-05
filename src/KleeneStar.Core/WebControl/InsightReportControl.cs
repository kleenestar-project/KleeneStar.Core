using KleeneStar.Core.WebInsight.Reports;
using System;
using System.Net;
using System.Text;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// The surface of an insight's reports tab: a toolbar to pick the report, the range and the
    /// interval, the chart, and the key figures beneath it.
    /// </summary>
    /// <remarks>
    /// The control renders the toolbar - translated, so the script needs no texts of its own -
    /// and an empty stage; <c>Assets/js/insightreports.js</c> loads the report from the address
    /// in <c>data-uri</c> and draws it with the Chart.js the framework ships on every page. The
    /// tab template is rendered once and cloned per tab, so the script initializes every copy it
    /// finds, including the ones the tab control adds later. The picked report is remembered per
    /// reader and insight in the browser.
    /// </remarks>
    public class InsightReportControl : Control
    {
        /// <summary>
        /// Gets or sets the address of the reports endpoint of the insight.
        /// </summary>
        public Func<IRenderControlContext, string> Uri { get; set; }

        /// <summary>
        /// Gets or sets the key the reader's choice is remembered under.
        /// </summary>
        public Func<IRenderControlContext, string> StorageKey { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="id">The id of the control.</param>
        public InsightReportControl(string id = null)
            : base(id)
        {
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            string T(string key) => WebUtility.HtmlEncode(I18N.Translate(renderContext, key));
            string A(string value) => WebUtility.HtmlEncode(value ?? string.Empty);

            var html = new StringBuilder();

            html.Append($"<div class=\"ks-insight-reports\" data-uri=\"{A(Uri?.Invoke(renderContext))}\" data-storage=\"{A(StorageKey?.Invoke(renderContext))}\">");
            html.Append("<div class=\"ks-insight-reports-toolbar\">");

            html.Append($"<label class=\"ks-insight-reports-field\"><span>{T("kleenestar.core:insight.report.picker.report")}</span><select class=\"ks-insight-reports-report\">");
            foreach (var key in InsightReports.Keys)
            {
                html.Append($"<option value=\"{A(key)}\">{T(InsightReports.TitleKey(key))}</option>");
            }
            html.Append("</select></label>");

            html.Append($"<label class=\"ks-insight-reports-field ks-insight-reports-period\"><span>{T("kleenestar.core:insight.report.picker.range")}</span><select class=\"ks-insight-reports-range\">");
            foreach (var (days, key) in new[] { (30, "30"), (90, "90"), (180, "180"), (365, "365") })
            {
                var selected = days == 90 ? " selected" : string.Empty;
                html.Append($"<option value=\"{days}\"{selected}>{T($"kleenestar.core:insight.report.range.{key}")}</option>");
            }
            html.Append("</select></label>");

            html.Append($"<label class=\"ks-insight-reports-field ks-insight-reports-period\"><span>{T("kleenestar.core:insight.report.picker.interval")}</span><select class=\"ks-insight-reports-interval\">");
            foreach (var key in new[] { "day", "week", "month" })
            {
                var selected = key == "week" ? " selected" : string.Empty;
                html.Append($"<option value=\"{key}\"{selected}>{T($"kleenestar.core:insight.report.interval.{key}")}</option>");
            }
            html.Append("</select></label>");

            html.Append($"<label class=\"ks-insight-reports-field ks-insight-reports-sprints\" hidden><span>{T("kleenestar.core:insight.report.picker.sprints")}</span><select class=\"ks-insight-reports-sprintcount\">");
            foreach (var count in new[] { 4, 8, 12, 24 })
            {
                var selected = count == 8 ? " selected" : string.Empty;
                html.Append($"<option value=\"{count}\"{selected}>{count}</option>");
            }
            html.Append("</select></label>");

            html.Append("</div>");
            html.Append("<p class=\"ks-insight-reports-description\"></p>");
            html.Append("<div class=\"ks-insight-reports-stage\"><canvas></canvas>");
            html.Append($"<div class=\"ks-insight-reports-empty\" hidden>{T("kleenestar.core:insight.report.empty")}</div>");
            html.Append($"<div class=\"ks-insight-reports-error\" hidden>{T("kleenestar.core:insight.report.error")}</div>");
            html.Append("</div>");
            html.Append("<p class=\"ks-insight-reports-notice\" hidden></p>");
            html.Append("<dl class=\"ks-insight-reports-summary\"></dl>");
            html.Append("</div>");

            return new HtmlRaw(html.ToString());
        }
    }
}
