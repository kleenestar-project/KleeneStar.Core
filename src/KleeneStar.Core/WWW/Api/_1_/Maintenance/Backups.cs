using KleeneStar.Core.WebMaintenance;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Core.WWW.Api._1_.Automation;
using System;
using System.IO;
using System.Linq;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Maintenance
{
    /// <summary>
    /// The backups of the database: <c>GET /api/1/maintenance/backups</c> lists them, <c>POST</c>
    /// writes one now - for the installation's administrators.
    /// </summary>
    /// <remarks>
    /// The endpoint never serves a backup's content: a copy of the whole database is not
    /// something to download through the application; it is taken from the server's disk.
    /// </remarks>
    [Title("kleenestar.core:maintenance.api.backups.title")]
    [Cache]
    public sealed class Backups : IRestApi
    {
        /// <summary>
        /// Handles <c>GET</c>.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The backups.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            if (!AccountAuthorization.IsAdministrator(request))
            {
                return new ResponseForbidden();
            }

            return AutomationApi.Json(new
            {
                directory = DatabaseBackups.Directory,
                backups = DatabaseBackups.List().Select(x => new { file = x.File, size = x.Size, created = x.Created })
            });
        }

        /// <summary>
        /// Handles <c>POST</c>: writes a backup now.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The backup, or why none was written.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Create(IRequest request)
        {
            if (!AccountAuthorization.IsAdministrator(request))
            {
                return new ResponseForbidden();
            }

            try
            {
                var backup = DatabaseBackups.Create(DateTime.UtcNow);

                return AutomationApi.Json(new { file = backup.File, size = backup.Size, created = backup.Created, integrity = backup.Integrity });
            }
            catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or IOException)
            {
                return AutomationApi.Refuse(ex.Message);
            }
        }
    }
}
