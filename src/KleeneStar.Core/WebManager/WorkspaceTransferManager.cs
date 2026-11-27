using KleeneStar.Core.WebPermission;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using KleeneStar.Model.Transfer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Exports and imports workspaces through the model's transfer (<see cref="ModelHub.ExportWorkspaces"/>,
    /// <see cref="ModelHub.ImportWorkspaces"/>), runs both as background jobs, and brings the
    /// installation up to date after an import: the path index, the relation registry and the
    /// visibility of the grants.
    /// </summary>
    /// <remarks>
    /// The jobs run one after the other on a single background task, never in the request that
    /// started them: an export of a large installation or an import that rewrites a workspace
    /// takes longer than a request should, and two imports of the same file must not both find
    /// the workspace absent. A job runs on behalf of the account that started it, so the audit
    /// log names it. The file of an export is written to <see cref="Directory"/> and removed
    /// with its job after <see cref="Retention"/> (housekeeping of the background worker).
    /// </remarks>
    public sealed class WorkspaceTransferManager : IWorkspaceTransferManager
    {
        /// <summary>
        /// How long a finished job and its file are kept.
        /// </summary>
        public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

        /// <summary>
        /// One import at a time, also when one is called directly.
        /// </summary>
        private static readonly object _importGate = new();

        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly ConcurrentDictionary<Guid, WorkspaceTransferJob> _jobs = new();
        private readonly ConcurrentQueue<WorkspaceTransferJob> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly CancellationTokenSource _stop = new();
        private readonly object _workerGate = new();
        private Task _worker;

        /// <summary>
        /// Raised for every workspace an export wrote.
        /// </summary>
        public event EventHandler<Workspace> WorkspaceExported;

        /// <summary>
        /// Raised for every workspace an import wrote.
        /// </summary>
        public event EventHandler<Workspace> WorkspaceImported;

        /// <summary>
        /// Returns the folder the export files are written to.
        /// </summary>
        public static string Directory => Path.GetFullPath(Path.Combine("data", "transfer"));

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private WorkspaceTransferManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Writes workspaces into a zip archive.
        /// </summary>
        /// <param name="workspaceIds">The workspaces; empty for all of them.</param>
        /// <param name="output">The stream the archive is written to.</param>
        /// <param name="progress">What is told of the progress (share, phase, detail), or <see langword="null"/>.</param>
        /// <returns>What was written.</returns>
        public WorkspaceTransferReport Export(IEnumerable<Guid> workspaceIds, Stream output, Action<double, string, string> progress = null)
        {
            ArgumentNullException.ThrowIfNull(output);

            var ids = (workspaceIds ?? []).Where(x => x != Guid.Empty).Distinct().ToList();

            if (ids.Count == 0)
            {
                ids = [.. CoreHub.WorkspaceManager.GetWorkspaces(new Query<Workspace>()).Select(x => x.Id)];
            }

            // the export reads the tables, not through the object manager: it is the whole
            // workspace, classified objects included - which is why only administrators reach it
            var report = ModelHub.ExportWorkspaces(ids, output, progress);

            foreach (var workspace in report.Workspaces)
            {
                if (CoreHub.WorkspaceManager.GetWorkspace(workspace.Id) is { } exported)
                {
                    WorkspaceExported?.Invoke(this, exported);
                }
            }

            return report;
        }

        /// <summary>
        /// Reads a zip archive into the database.
        /// </summary>
        /// <param name="input">The archive.</param>
        /// <param name="options">The choices of the import.</param>
        /// <param name="progress">What is told of the progress (share, phase, detail), or <see langword="null"/>.</param>
        /// <returns>What was imported, left out and left empty.</returns>
        public WorkspaceTransferReport Import(Stream input, WorkspaceImportOptions options = null, Action<double, string, string> progress = null)
        {
            ArgumentNullException.ThrowIfNull(input);

            WorkspaceTransferReport report;

            lock (_importGate)
            {
                report = ModelHub.ImportWorkspaces(input, options, progress);

                if (!report.Succeeded || options?.DryRun == true)
                {
                    return report;
                }

                // what the rows imply beyond themselves
                progress?.Invoke(0.97, "import.finish", null);
                ObjectPathIndex.Rebuild(_httpServerContext?.Log);
                CoreHub.ObjectRelationTypeManager?.Publish();
                ContentVisibility.Invalidate();
            }

            foreach (var workspace in report.Workspaces.Where(x => x.Skipped is null))
            {
                if (CoreHub.WorkspaceManager.GetWorkspace(workspace.Id) is { } imported)
                {
                    WorkspaceImported?.Invoke(this, imported);
                }
            }

            return report;
        }

        /// <summary>
        /// Starts an export in the background.
        /// </summary>
        /// <param name="workspaceIds">The workspaces; empty for all of them.</param>
        /// <param name="ownerId">The account the job runs for.</param>
        /// <returns>The job.</returns>
        public WorkspaceTransferJob StartExport(IEnumerable<Guid> workspaceIds, Guid ownerId)
        {
            return Enqueue(new WorkspaceTransferJob
            {
                IsExport = true,
                OwnerId = ownerId,
                WorkspaceIds = [.. (workspaceIds ?? []).Where(x => x != Guid.Empty).Distinct()]
            });
        }

        /// <summary>
        /// Starts an import in the background.
        /// </summary>
        /// <param name="content">The archive.</param>
        /// <param name="options">The choices of the import.</param>
        /// <param name="ownerId">The account the job runs for.</param>
        /// <returns>The job.</returns>
        public WorkspaceTransferJob StartImport(byte[] content, WorkspaceImportOptions options, Guid ownerId)
        {
            ArgumentNullException.ThrowIfNull(content);

            return Enqueue(new WorkspaceTransferJob
            {
                IsExport = false,
                DryRun = options?.DryRun == true,
                OwnerId = ownerId,
                Options = options ?? new WorkspaceImportOptions(),
                Content = content
            });
        }

        /// <summary>
        /// Returns a job, or <see langword="null"/> when it is unknown.
        /// </summary>
        /// <param name="jobId">The id.</param>
        /// <returns>The job.</returns>
        public WorkspaceTransferJob GetJob(Guid jobId)
        {
            return _jobs.TryGetValue(jobId, out var job) ? job : null;
        }

        /// <summary>
        /// Returns the jobs, the latest first.
        /// </summary>
        /// <returns>The jobs.</returns>
        public IEnumerable<WorkspaceTransferJob> GetJobs()
        {
            return _jobs.Values.OrderByDescending(x => x.Created).ToList();
        }

        /// <summary>
        /// Forgets the jobs that finished longer ago than the retention and removes their files,
        /// as well as files a previous run of the installation left behind.
        /// </summary>
        /// <param name="now">The current time.</param>
        public void Prune(DateTime now)
        {
            foreach (var job in _jobs.Values.Where(x => x.Finished is { } finished && now - finished > Retention).ToList())
            {
                _jobs.TryRemove(job.Id, out _);
                Delete(job.FilePath);
            }

            if (!System.IO.Directory.Exists(Directory))
            {
                return;
            }

            var known = _jobs.Values.Select(x => x.FilePath).Where(x => x is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var file in new DirectoryInfo(Directory).GetFiles("*.zip"))
            {
                if (!known.Contains(file.FullName) && now - file.LastWriteTimeUtc > Retention)
                {
                    Delete(file.FullName);
                }
            }
        }

        /// <summary>
        /// Returns the file name of an export: the key of a single workspace, or a date.
        /// </summary>
        /// <param name="report">The report of the export.</param>
        /// <returns>The file name.</returns>
        public static string FileName(WorkspaceTransferReport report)
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
            var name = report.Workspaces.Count == 1 && !string.IsNullOrWhiteSpace(report.Workspaces[0].Key)
                ? report.Workspaces[0].Key
                : "workspaces";

            var safe = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == '"' || char.IsWhiteSpace(c) ? '_' : c));

            return $"kleenestar-{safe}-{stamp}.zip";
        }

        /// <summary>
        /// Releases all resources used by the instance: the background task stops after the
        /// job it runs.
        /// </summary>
        public void Dispose()
        {
            _stop.Cancel();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Registers a job and wakes the background task.
        /// </summary>
        private WorkspaceTransferJob Enqueue(WorkspaceTransferJob job)
        {
            _jobs[job.Id] = job;
            _queue.Enqueue(job);

            lock (_workerGate)
            {
                _worker ??= Task.Factory.StartNew(Work, _stop.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            _signal.Release();

            return job;
        }

        /// <summary>
        /// Runs the queued jobs one after the other until the manager is disposed.
        /// </summary>
        private void Work()
        {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    _signal.Wait(_stop.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                while (_queue.TryDequeue(out var job))
                {
                    Run(job);
                }
            }
        }

        /// <summary>
        /// Runs a job on behalf of its owner and records how it ended.
        /// </summary>
        private void Run(WorkspaceTransferJob job)
        {
            using var identity = job.OwnerId != Guid.Empty ? CoreHub.SessionManager.BeginIdentity(job.OwnerId) : null;

            try
            {
                job.Advance(0, job.IsExport ? "export.start" : "import.read", null);

                if (job.IsExport)
                {
                    System.IO.Directory.CreateDirectory(Directory);

                    var path = Path.Combine(Directory, job.Id.ToString("N") + ".zip");
                    WorkspaceTransferReport report;

                    using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        report = Export(job.WorkspaceIds, file, job.Advance);
                    }

                    job.FilePath = path;
                    job.Size = new FileInfo(path).Length;
                    job.FileName = FileName(report);
                    job.Finish(report, failed: false);
                }
                else
                {
                    using var stream = new MemoryStream(job.Content, writable: false);

                    job.Finish(Import(stream, job.Options, job.Advance), failed: false);
                }
            }
            catch (Exception ex)
            {
                _httpServerContext?.Log?.Exception(ex);

                Delete(job.FilePath ?? (job.IsExport ? Path.Combine(Directory, job.Id.ToString("N") + ".zip") : null));
                job.FilePath = null;

                var report = new WorkspaceTransferReport { Error = "job-failed" };
                report.Note("write-failed", detail: (ex.InnerException ?? ex).Message);

                job.Finish(report, failed: true);
            }
        }

        /// <summary>
        /// Removes a file, if it is there.
        /// </summary>
        private void Delete(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException ex)
            {
                _httpServerContext?.Log?.Exception(ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                _httpServerContext?.Log?.Exception(ex);
            }
        }
    }
}
