using KleeneStar.Model.Entities;
using KleeneStar.Model.Transfer;
using System;
using System.Collections.Generic;
using System.IO;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Exports workspaces with everything that belongs to them into a zip archive and imports
    /// such an archive - written by this or by another version of the application.
    /// </summary>
    /// <remarks>
    /// What belongs to a workspace and what does not is the model's transfer catalog
    /// (<see cref="WorkspaceTransferCatalog"/>): accounts, groups, tenants, per-user state and
    /// logs are never exported. The settings page runs both as background jobs
    /// (<see cref="StartExport"/>, <see cref="StartImport"/>) and follows their progress; the
    /// synchronous <see cref="Export"/> and <see cref="Import"/> are what a job runs.
    /// See <c>kleenestar.transfer.md</c>.
    /// </remarks>
    public interface IWorkspaceTransferManager : IComponentManager
    {
        /// <summary>
        /// Raised for every workspace an export wrote.
        /// </summary>
        event EventHandler<Workspace> WorkspaceExported;

        /// <summary>
        /// Raised for every workspace an import wrote.
        /// </summary>
        event EventHandler<Workspace> WorkspaceImported;

        /// <summary>
        /// Writes workspaces into a zip archive.
        /// </summary>
        /// <param name="workspaceIds">The workspaces; empty for all of them.</param>
        /// <param name="output">The stream the archive is written to.</param>
        /// <param name="progress">What is told of the progress (share, phase, detail), or <see langword="null"/>.</param>
        /// <param name="structureOnly">Whether objects and their dependent data are omitted.</param>
        /// <returns>What was written.</returns>
        WorkspaceTransferReport Export(IEnumerable<Guid> workspaceIds, Stream output, Action<double, string, string> progress = null, bool structureOnly = false);

        /// <summary>
        /// Reads a zip archive into the database.
        /// </summary>
        /// <param name="input">The archive.</param>
        /// <param name="options">The choices of the import.</param>
        /// <param name="progress">What is told of the progress (share, phase, detail), or <see langword="null"/>.</param>
        /// <returns>What was imported, left out and left empty.</returns>
        WorkspaceTransferReport Import(Stream input, WorkspaceImportOptions options = null, Action<double, string, string> progress = null);

        /// <summary>
        /// Starts an export in the background.
        /// </summary>
        /// <param name="workspaceIds">The workspaces; empty for all of them.</param>
        /// <param name="ownerId">The account the job runs for.</param>
        /// <param name="structureOnly">Whether objects and their dependent data are omitted.</param>
        /// <returns>The job.</returns>
        WorkspaceTransferJob StartExport(IEnumerable<Guid> workspaceIds, Guid ownerId, bool structureOnly = false);

        /// <summary>
        /// Starts an import in the background.
        /// </summary>
        /// <param name="content">The archive.</param>
        /// <param name="options">The choices of the import.</param>
        /// <param name="ownerId">The account the job runs for.</param>
        /// <returns>The job.</returns>
        WorkspaceTransferJob StartImport(byte[] content, WorkspaceImportOptions options, Guid ownerId);

        /// <summary>
        /// Returns a job, or <see langword="null"/> when it is unknown.
        /// </summary>
        /// <param name="jobId">The id.</param>
        /// <returns>The job.</returns>
        WorkspaceTransferJob GetJob(Guid jobId);

        /// <summary>
        /// Returns the jobs, the latest first.
        /// </summary>
        /// <returns>The jobs.</returns>
        IEnumerable<WorkspaceTransferJob> GetJobs();

        /// <summary>
        /// Forgets the jobs that finished longer ago than the retention and removes their files.
        /// </summary>
        /// <param name="now">The current time.</param>
        void Prune(DateTime now);
    }
}
