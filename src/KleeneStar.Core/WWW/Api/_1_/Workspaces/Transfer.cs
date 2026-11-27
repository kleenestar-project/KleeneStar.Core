using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebReport;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Transfer;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Workspaces
{
    /// <summary>
    /// The export and import of workspaces as background jobs, for the installation's
    /// administrators.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><c>POST ?action=export&amp;ids=…</c> starts an export (all workspaces without
    /// <c>ids</c>) and answers the job.</item>
    /// <item><c>POST ?action=import&amp;dryrun=…&amp;overwrite=…&amp;ids=…</c> with the archive as
    /// the body starts an import and answers the job. The forms of the framework post no file,
    /// so the settings page sends the archive itself as the body.</item>
    /// <item><c>GET ?job=…</c> answers the progress of a job, and its report once finished.</item>
    /// <item><c>GET ?job=…&amp;download=true</c> answers the file of a finished export.</item>
    /// <item><c>GET</c> without a job answers the latest jobs, so the page picks up a job that
    /// still runs after a reload.</item>
    /// </list>
    /// Nothing of the work happens in the request: the request only starts or reads a job of
    /// <see cref="IWorkspaceTransferManager"/>.
    /// </remarks>
    [Title("kleenestar.core:workspace.transfer.title")]
    public sealed class Transfer : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Transfer()
        {
        }

        /// <summary>
        /// Handles <c>GET</c>: a job, the file of an export, or the latest jobs.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The answer.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            if (!AccountAuthorization.IsAdministrator(request))
            {
                return new ResponseForbidden();
            }

            var jobText = request.GetParameter("job")?.Value;

            if (string.IsNullOrWhiteSpace(jobText))
            {
                return JsonApi.Json(new
                {
                    jobs = CoreHub.WorkspaceTransferManager.GetJobs().Take(10).Select(x => Describe(request, x))
                });
            }

            var job = JsonApi.ParseId(jobText) is Guid id ? CoreHub.WorkspaceTransferManager.GetJob(id) : null;

            if (job is null)
            {
                return new ResponseNotFound();
            }

            if (string.Equals(request.GetParameter("download")?.Value, "true", StringComparison.OrdinalIgnoreCase))
            {
                if (!job.HasFile)
                {
                    return new ResponseNotFound();
                }

                return ObjectExport.Attachment(File.ReadAllBytes(job.FilePath), "application/zip", job.FileName);
            }

            return JsonApi.Json(Describe(request, job));
        }

        /// <summary>
        /// Handles <c>POST</c>: starts an export or an import.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The job.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Start(IRequest request)
        {
            if (!AccountAuthorization.IsAdministrator(request))
            {
                return new ResponseForbidden();
            }

            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var ids = Ids(request.GetParameter("ids")?.Value);
            var action = request.GetParameter("action")?.Value;

            if (string.Equals(action, "export", StringComparison.OrdinalIgnoreCase))
            {
                return JsonApi.Json(Describe(request, CoreHub.WorkspaceTransferManager.StartExport(ids, ownerId)));
            }

            var content = (request as Request)?.Content;

            if (content is not { Length: > 0 })
            {
                return JsonApi.Refuse(I18N.Translate(request, "kleenestar.core:workspace.transfer.error.empty"));
            }

            var job = CoreHub.WorkspaceTransferManager.StartImport(content, new WorkspaceImportOptions
            {
                DryRun = Flag(request, "dryrun"),
                Overwrite = Flag(request, "overwrite"),
                WorkspaceIds = ids.Length > 0 ? ids : null
            }, ownerId);

            return JsonApi.Json(Describe(request, job));
        }

        /// <summary>
        /// Returns a job in the words of the caller.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="job">The job.</param>
        /// <returns>The payload.</returns>
        public static object Describe(IRequest request, WorkspaceTransferJob job)
        {
            string T(string key, params object[] args) => I18N.Translate(request, "kleenestar.core:workspace.transfer." + key, args);

            var address = request.Uri?.ToString() ?? string.Empty;
            var query = address.IndexOf('?');
            var self = query >= 0 ? address[..query] : address;

            return new
            {
                id = job.Id,
                kind = job.IsExport ? "export" : "import",
                dryRun = job.DryRun,
                state = job.State.ToString().ToLowerInvariant(),
                progress = Math.Round(job.Progress * 100, 1),
                phase = T("phase." + Key(job.Phase), job.Detail ?? string.Empty),
                created = job.Created,
                finished = job.Finished,
                download = job.HasFile ? $"{self}?job={job.Id}&download=true" : null,
                fileName = job.HasFile ? job.FileName : null,
                size = job.HasFile ? job.Size : 0,
                report = job.Report is null ? null : Describe(request, job.Report, job.DryRun)
            };
        }

        /// <summary>
        /// Returns a report in the words of the caller.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="report">The report.</param>
        /// <param name="dryRun">Whether the file was only checked.</param>
        /// <returns>The payload.</returns>
        public static object Describe(IRequest request, WorkspaceTransferReport report, bool dryRun)
        {
            string T(string key, params object[] args) => I18N.Translate(request, "kleenestar.core:workspace.transfer." + key, args);

            return new
            {
                succeeded = report.Succeeded,
                dryRun,
                error = report.Error is null ? null : T("error." + Key(report.Error)),
                formatVersion = report.FormatVersion,
                schema = report.Schema,
                workspaces = report.Workspaces.Select(x => new
                {
                    id = x.Id,
                    key = x.Key,
                    name = x.Name,
                    imported = x.Skipped is null,
                    replaced = x.Replaced,
                    reason = x.Skipped is null ? null : Reason(x.Skipped, T)
                }),
                tables = report.Tables
                    .Where(x => x.Rows > 0 || x.Deleted > 0)
                    .Select(x => new { name = x.Name, rows = x.Rows, written = x.Written, updated = x.Updated, deleted = x.Deleted, reused = x.Reused, skipped = x.Skipped }),
                notes = report.Notes.Select(x => T("note." + Key(x.Code), x.Table ?? string.Empty, x.Detail ?? string.Empty, x.Count.ToString(CultureInfo.InvariantCulture)))
            };
        }

        /// <summary>
        /// Returns the file name of an export (see <see cref="WorkspaceTransferManager.FileName"/>).
        /// </summary>
        /// <param name="report">The report of the export.</param>
        /// <returns>The file name.</returns>
        public static string FileName(WorkspaceTransferReport report)
        {
            return WorkspaceTransferManager.FileName(report);
        }

        /// <summary>
        /// Returns why a workspace was not imported.
        /// </summary>
        private static string Reason(string skipped, Func<string, object[], string> translate)
        {
            var parts = skipped.Split(':', 2);

            return translate("skipped." + Key(parts[0]), [parts.Length > 1 ? parts[1] : string.Empty]);
        }

        /// <summary>
        /// Returns the ids of a comma separated list.
        /// </summary>
        private static Guid[] Ids(string text)
        {
            return [.. (text ?? string.Empty)
                .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(JsonApi.ParseId)
                .Where(x => x.HasValue)
                .Select(x => x.Value)];
        }

        /// <summary>
        /// Returns whether a parameter is <c>true</c>.
        /// </summary>
        private static bool Flag(IRequest request, string name)
        {
            return string.Equals(request.GetParameter(name)?.Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the translation key segment of a code.
        /// </summary>
        private static string Key(string code)
        {
            return (code ?? string.Empty).Replace('-', '.');
        }
    }
}
