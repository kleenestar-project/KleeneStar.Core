using KleeneStar.Model.Transfer;
using System;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The state of a transfer job.
    /// </summary>
    public enum WorkspaceTransferJobState
    {
        /// <summary>
        /// Waits for the jobs before it.
        /// </summary>
        Queued,

        /// <summary>
        /// Runs.
        /// </summary>
        Running,

        /// <summary>
        /// Finished; the report says how far it went.
        /// </summary>
        Finished,

        /// <summary>
        /// Stopped by an error the report does not cover.
        /// </summary>
        Failed
    }

    /// <summary>
    /// An export or an import of workspaces that runs in the background: started by a request,
    /// followed by the page through its progress, and - for an export - the file to download.
    /// </summary>
    /// <remarks>
    /// Jobs live in the memory of the installation that runs them; a restart forgets them, and
    /// the file of an export is removed after <see cref="WorkspaceTransferManager.Retention"/>.
    /// </remarks>
    public sealed class WorkspaceTransferJob
    {
        private readonly object _gate = new();

        /// <summary>
        /// Returns the id.
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// Returns whether it is an export (<see langword="true"/>) or an import.
        /// </summary>
        public bool IsExport { get; init; }

        /// <summary>
        /// Returns whether the import only checks the file.
        /// </summary>
        public bool DryRun { get; init; }

        /// <summary>
        /// Returns the account that started it; the job runs on its behalf, so the audit log
        /// names it.
        /// </summary>
        public Guid OwnerId { get; init; }

        /// <summary>
        /// Returns when it was started.
        /// </summary>
        public DateTime Created { get; } = DateTime.UtcNow;

        /// <summary>
        /// Returns when it finished, or <see langword="null"/>.
        /// </summary>
        public DateTime? Finished { get; private set; }

        /// <summary>
        /// Returns the state.
        /// </summary>
        public WorkspaceTransferJobState State { get; private set; } = WorkspaceTransferJobState.Queued;

        /// <summary>
        /// Returns the share done, 0 to 1.
        /// </summary>
        public double Progress { get; private set; }

        /// <summary>
        /// Returns the phase (<c>queued</c>, <c>export.table</c>, <c>import.read</c>, …).
        /// </summary>
        public string Phase { get; private set; } = "queued";

        /// <summary>
        /// Returns the detail of the phase (a table), or <see langword="null"/>.
        /// </summary>
        public string Detail { get; private set; }

        /// <summary>
        /// Returns the report, once finished.
        /// </summary>
        public WorkspaceTransferReport Report { get; private set; }

        /// <summary>
        /// Returns the name the export file is downloaded under.
        /// </summary>
        public string FileName { get; internal set; }

        /// <summary>
        /// Returns the size of the export file in bytes.
        /// </summary>
        public long Size { get; internal set; }

        /// <summary>
        /// Returns the path of the export file on the server.
        /// </summary>
        internal string FilePath { get; set; }

        /// <summary>
        /// Returns the archive an import reads; dropped once it ran.
        /// </summary>
        internal byte[] Content { get; set; }

        /// <summary>
        /// Returns the choices of an import.
        /// </summary>
        internal WorkspaceImportOptions Options { get; init; }

        /// <summary>
        /// Returns the workspaces of an export (empty for all).
        /// </summary>
        internal Guid[] WorkspaceIds { get; init; } = [];

        /// <summary>
        /// Returns whether the export omits objects and their dependent data.
        /// </summary>
        internal bool StructureOnly { get; init; }

        /// <summary>
        /// Returns whether an export file is ready to download.
        /// </summary>
        public bool HasFile => IsExport && State == WorkspaceTransferJobState.Finished && FilePath is not null && System.IO.File.Exists(FilePath);

        /// <summary>
        /// Records the progress.
        /// </summary>
        /// <param name="share">The share done.</param>
        /// <param name="phase">The phase.</param>
        /// <param name="detail">The detail.</param>
        internal void Advance(double share, string phase, string detail)
        {
            lock (_gate)
            {
                State = WorkspaceTransferJobState.Running;
                Progress = Math.Clamp(Math.Max(Progress, share), 0, 1);
                Phase = phase ?? Phase;
                Detail = detail;
            }
        }

        /// <summary>
        /// Records the end.
        /// </summary>
        /// <param name="report">The report.</param>
        /// <param name="failed">Whether an error stopped it.</param>
        internal void Finish(WorkspaceTransferReport report, bool failed)
        {
            lock (_gate)
            {
                Report = report;
                State = failed ? WorkspaceTransferJobState.Failed : WorkspaceTransferJobState.Finished;
                Progress = 1;
                Phase = "done";
                Detail = null;
                Finished = DateTime.UtcNow;
                Content = null;
            }
        }
    }
}
