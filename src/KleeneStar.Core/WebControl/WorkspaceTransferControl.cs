using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// The export tab of the workspace transfer settings page: the workspaces as selectable cards
    /// with a filter, what an export holds and what it leaves out, and the download.
    /// </summary>
    /// <remarks>
    /// <c>Assets/js/kleenestar.core.workspace.transfer.export.js</c> (<c>wx-kleenestar-transferexport</c>)
    /// keeps the selection, the count and the download address (<c>data-uri</c> with the ids).
    /// </remarks>
    public class WorkspaceTransferExportControl : Control
    {
        /// <summary>
        /// Returns or sets the address of the transfer endpoint.
        /// </summary>
        public Func<IRenderControlContext, string> Uri { get; set; }

        /// <summary>
        /// Returns or sets the workspaces offered for the export.
        /// </summary>
        public Func<IRenderControlContext, IEnumerable<Workspace>> Workspaces { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="id">The id of the control.</param>
        public WorkspaceTransferExportControl(string id = null)
            : base(id)
        {
        }

        /// <summary>
        /// Converts the control to HTML.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>The HTML node.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            string T(string key) => WorkspaceTransferMarkup.T(renderContext, key);
            var A = WorkspaceTransferMarkup.A;
            var workspaces = (Workspaces?.Invoke(renderContext) ?? []).ToList();
            var html = new StringBuilder();

            html.Append($"<div class=\"ks-transfer ks-transfer-export wx-kleenestar-transferexport\" data-uri=\"{A(Uri?.Invoke(renderContext))}\"");
            html.Append($" data-selected=\"{T("export.selected")}\" data-none=\"{T("error.noselection")}\" data-failed=\"{T("error.request")}\" data-workspaces=\"{T("export.workspaces")}\">");

            html.Append($"<p class=\"ks-transfer-lede\">{T("export.description")}</p>");

            // the job comes first: what runs or is ready to download is what the reader looks for
            html.Append(WorkspaceTransferMarkup.Job(renderContext, export: true));

            // the choice
            html.Append("<div class=\"ks-transfer-toolbar\">");
            html.Append("<div class=\"ks-transfer-search\">");
            html.Append("<i class=\"wx-icon-light wx-icon-light-magnifying-glass\" aria-hidden=\"true\"></i>");
            html.Append($"<input type=\"search\" class=\"form-control form-control-sm ks-transfer-filter\" placeholder=\"{T("export.filter")}\" aria-label=\"{T("export.filter")}\">");
            html.Append("</div>");
            html.Append("<div class=\"ks-transfer-toolbar-actions\">");
            html.Append($"<button type=\"button\" class=\"btn btn-sm btn-link ks-transfer-all\">{T("export.all")}</button>");
            html.Append($"<button type=\"button\" class=\"btn btn-sm btn-link ks-transfer-none\">{T("export.none")}</button>");
            html.Append("</div>");
            html.Append("<span class=\"ks-transfer-count\" aria-live=\"polite\"></span>");
            html.Append("</div>");

            html.Append("<div class=\"ks-transfer-grid\">");

            foreach (var workspace in workspaces)
            {
                var teaser = ProseText.ToPlainText(workspace.Description ?? string.Empty);

                if (teaser.Length > 140)
                {
                    teaser = teaser[..140].TrimEnd() + "…";
                }

                html.Append($"<label class=\"ks-transfer-card\" data-search=\"{A((workspace.Name + " " + workspace.Key).ToLowerInvariant())}\">");
                html.Append($"<input type=\"checkbox\" class=\"form-check-input ks-transfer-workspace\" value=\"{workspace.Id}\" checked>");
                html.Append(WorkspaceTransferMarkup.Avatar(workspace));
                html.Append("<span class=\"ks-transfer-card-body\">");
                html.Append($"<span class=\"ks-transfer-card-title\">{A(workspace.Name)}</span>");
                html.Append($"<span class=\"ks-transfer-card-meta\"><span class=\"ks-transfer-chip\">{A(workspace.Key)}</span>");

                if (workspace.State != default)
                {
                    html.Append($"<span class=\"ks-transfer-chip ks-transfer-chip-muted\">{A(workspace.State.ToString())}</span>");
                }

                html.Append("</span>");

                if (!string.IsNullOrWhiteSpace(teaser))
                {
                    html.Append($"<span class=\"ks-transfer-card-teaser\">{A(teaser)}</span>");
                }

                html.Append("</span>");
                html.Append("</label>");
            }

            html.Append("</div>");
            html.Append($"<p class=\"ks-transfer-empty\" hidden>{T("export.nomatch")}</p>");

            // the export scope
            html.Append("<div class=\"ks-transfer-option\">");
            html.Append("<div class=\"form-check form-switch\">");
            html.Append("<input class=\"form-check-input ks-transfer-structureonly\" type=\"checkbox\" role=\"switch\" id=\"ks-transfer-structureonly\">");
            html.Append($"<label class=\"form-check-label\" for=\"ks-transfer-structureonly\">{T("export.structureonly")}</label>");
            html.Append("</div>");
            html.Append($"<div class=\"ks-transfer-option-help\">{T("export.structureonly.help")}</div>");
            html.Append("</div>");

            // what travels and what stays
            html.Append("<div class=\"ks-transfer-scope\">");
            html.Append("<div class=\"ks-transfer-scope-col ks-transfer-scope-in\">");
            html.Append($"<div class=\"ks-transfer-scope-title\"><i class=\"wx-icon-light wx-icon-light-circle-check\" aria-hidden=\"true\"></i>{T("export.included")}</div>");
            html.Append($"<p class=\"ks-transfer-included\" data-full=\"{T("export.included.list")}\" data-structure=\"{T("export.structureonly.included")}\">{T("export.included.list")}</p>");
            html.Append("</div>");
            html.Append("<div class=\"ks-transfer-scope-col ks-transfer-scope-out\">");
            html.Append($"<div class=\"ks-transfer-scope-title\"><i class=\"wx-icon-light wx-icon-light-circle-minus\" aria-hidden=\"true\"></i>{T("export.excluded")}</div>");
            html.Append($"<p>{T("export.excluded.list")}</p>");
            html.Append("</div>");
            html.Append("</div>");

            // the start, the progress and the download
            html.Append("<div class=\"ks-transfer-actions\">");
            html.Append($"<button type=\"button\" class=\"btn btn-primary ks-transfer-start\"><i class=\"wx-icon-light wx-icon-light-file-export\" aria-hidden=\"true\"></i><span>{T("export.button")}</span><span class=\"badge ks-transfer-badge\"></span></button>");
            html.Append($"<span class=\"ks-transfer-hint\"><i class=\"wx-icon-light wx-icon-light-file-zipper\" aria-hidden=\"true\"></i>{T("export.hint")}</span>");
            html.Append("<span class=\"ks-transfer-message\" role=\"alert\"></span>");
            html.Append("</div>");

            html.Append("</div>");

            return new HtmlRaw(html.ToString());
        }
    }

    /// <summary>
    /// The import tab of the workspace transfer settings page: a drop zone for the export file,
    /// the choice to overwrite present workspaces, the check and the import, and the report.
    /// </summary>
    /// <remarks>
    /// <c>Assets/js/kleenestar.core.workspace.transfer.import.js</c> (<c>wx-kleenestar-transferimport</c>)
    /// sends the chosen archive as the body of a POST to <c>data-uri</c> - the framework forms
    /// post no file - only when "Check" or "Import" is pressed, and asks for a confirmation
    /// before an import that may overwrite workspaces.
    /// </remarks>
    public class WorkspaceTransferImportControl : Control
    {
        /// <summary>
        /// Returns or sets the address of the transfer endpoint.
        /// </summary>
        public Func<IRenderControlContext, string> Uri { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="id">The id of the control.</param>
        public WorkspaceTransferImportControl(string id = null)
            : base(id)
        {
        }

        /// <summary>
        /// Converts the control to HTML.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>The HTML node.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            string T(string key) => WorkspaceTransferMarkup.T(renderContext, key);
            var A = WorkspaceTransferMarkup.A;
            var html = new StringBuilder();
            var words = new[]
            {
                "busy.check", "busy.import", "error.request", "error.nofile", "error.notzip", "result.imported", "result.checked",
                "result.new", "result.replaced", "result.skipped", "result.table", "result.rows", "result.written",
                "result.updated", "result.deleted", "result.reused", "result.left", "result.notes", "result.details",
                "confirm.text", "confirm.generic", "confirm.button", "confirm.cancel", "file.size", "result.ready"
            };

            html.Append($"<div class=\"ks-transfer ks-transfer-import wx-kleenestar-transferimport\" data-uri=\"{A(Uri?.Invoke(renderContext))}\"");

            foreach (var word in words)
            {
                html.Append($" data-{word.Replace('.', '-')}=\"{T(word)}\"");
            }

            html.Append('>');

            html.Append($"<p class=\"ks-transfer-lede\">{T("import.description")}</p>");

            // the job and its report come first, as on the export tab
            html.Append(WorkspaceTransferMarkup.Job(renderContext, export: false));
            html.Append("<div class=\"ks-transfer-result\" aria-live=\"polite\"></div>");

            // the file
            html.Append("<label class=\"ks-transfer-drop\" tabindex=\"0\">");
            html.Append("<input type=\"file\" class=\"ks-transfer-file\" accept=\".zip,application/zip\" hidden>");
            html.Append("<i class=\"wx-icon-light wx-icon-light-cloud-arrow-up ks-transfer-drop-icon\" aria-hidden=\"true\"></i>");
            html.Append($"<span class=\"ks-transfer-drop-title\">{T("import.drop")}</span>");
            html.Append($"<span class=\"ks-transfer-drop-hint\">{T("import.drop.hint")}</span>");
            html.Append("<span class=\"ks-transfer-drop-file\" hidden><i class=\"wx-icon-light wx-icon-light-file-zipper\" aria-hidden=\"true\"></i><span class=\"ks-transfer-drop-name\"></span><span class=\"ks-transfer-drop-size\"></span></span>");
            html.Append("</label>");

            // the choice
            html.Append("<div class=\"ks-transfer-option\">");
            html.Append("<div class=\"form-check form-switch\">");
            html.Append("<input class=\"form-check-input ks-transfer-overwrite\" type=\"checkbox\" role=\"switch\" id=\"ks-transfer-overwrite\" checked>");
            html.Append($"<label class=\"form-check-label\" for=\"ks-transfer-overwrite\">{T("import.overwrite")}</label>");
            html.Append("</div>");
            html.Append($"<div class=\"ks-transfer-option-help\">{T("import.overwrite.help")}</div>");
            html.Append("</div>");

            html.Append("<div class=\"ks-transfer-actions\">");
            html.Append($"<button type=\"button\" class=\"btn btn-outline-secondary ks-transfer-check\" disabled><i class=\"wx-icon-light wx-icon-light-list-check\" aria-hidden=\"true\"></i><span>{T("import.check")}</span></button>");
            html.Append($"<button type=\"button\" class=\"btn btn-primary ks-transfer-run\" disabled><i class=\"wx-icon-light wx-icon-light-file-import\" aria-hidden=\"true\"></i><span>{T("import.button")}</span></button>");
            html.Append("</div>");

            html.Append("</div>");

            return new HtmlRaw(html.ToString());
        }
    }

    /// <summary>
    /// What the two tabs of the workspace transfer share.
    /// </summary>
    internal static class WorkspaceTransferMarkup
    {
        /// <summary>
        /// Returns a translated text of the transfer, encoded for HTML.
        /// </summary>
        public static string T(IRenderControlContext renderContext, string key)
        {
            return WebUtility.HtmlEncode(I18N.Translate(renderContext, "kleenestar.core:workspace.transfer." + key));
        }

        /// <summary>
        /// Returns a value encoded for an HTML attribute.
        /// </summary>
        public static string A(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        /// <summary>
        /// Returns the progress panel of a job: phase, share and bar, and for an export the
        /// download once the file is ready.
        /// </summary>
        public static string Job(IRenderControlContext renderContext, bool export)
        {
            var html = new StringBuilder();

            html.Append("<div class=\"ks-transfer-job\" hidden>");
            html.Append("<div class=\"ks-transfer-job-head\">");
            html.Append("<span class=\"ks-transfer-job-phase\"></span>");
            html.Append("<span class=\"ks-transfer-job-percent\"></span>");
            html.Append("</div>");
            html.Append("<div class=\"progress ks-transfer-progress\" role=\"progressbar\" aria-valuemin=\"0\" aria-valuemax=\"100\" aria-valuenow=\"0\">");
            html.Append("<div class=\"progress-bar progress-bar-striped progress-bar-animated ks-transfer-progress-bar\" style=\"width: 0%\"></div>");
            html.Append("</div>");

            if (export)
            {
                html.Append("<div class=\"ks-transfer-job-ready\" hidden>");
                html.Append("<i class=\"wx-icon-light wx-icon-light-circle-check ks-transfer-job-ready-icon\" aria-hidden=\"true\"></i>");
                html.Append("<span class=\"ks-transfer-job-ready-body\">");
                html.Append($"<span class=\"ks-transfer-job-ready-title\">{T(renderContext, "result.ready")}</span>");
                html.Append("<span class=\"ks-transfer-job-ready-meta\"></span>");
                html.Append("</span>");
                html.Append($"<a class=\"btn btn-success ks-transfer-download\" href=\"#\" download><i class=\"wx-icon-light wx-icon-light-download\" aria-hidden=\"true\"></i><span>{T(renderContext, "export.download")}</span></a>");
                html.Append("</div>");
            }

            html.Append("<div class=\"ks-transfer-job-error\" role=\"alert\" hidden></div>");
            html.Append("</div>");

            return html.ToString();
        }

        /// <summary>
        /// Returns the picture of a workspace: its icon, or its key's first letters.
        /// </summary>
        public static string Avatar(Workspace workspace)
        {
            var uri = workspace.Icon?.Uri?.ToString();

            if (!string.IsNullOrWhiteSpace(uri))
            {
                return $"<span class=\"ks-transfer-avatar\"><img src=\"{A(uri)}\" alt=\"\"></span>";
            }

            var letters = new string((workspace.Key ?? workspace.Name ?? "?").Where(char.IsLetterOrDigit).Take(2).ToArray()).ToUpperInvariant();

            return $"<span class=\"ks-transfer-avatar ks-transfer-avatar-letters\">{A(letters)}</span>";
        }
    }
}
