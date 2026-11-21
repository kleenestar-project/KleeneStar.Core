using KleeneStar.Core.WebAttribute;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.WatcherUsers._objectkey_
{
    /// <summary>
    /// REST endpoint that supplies the candidate user directory consumed by the
    /// <c>ControlDataWatcher</c> "+" dropdown on the watcher card. The URL is
    /// <c>/api/1/watcher-users/{objectkey}</c>; the <c>{objectkey}</c> URL segment
    /// is declared via <see cref="ObjectKeySegmentAttribute"/> but is intentionally
    /// ignored by this endpoint — every object currently exposes the same global
    /// identity directory. The segment is kept so the URI templates of the two watcher
    /// endpoints stay symmetrical (both bind the same
    /// <see cref="WebParameter.ObjectKeyParameter"/>).
    /// </summary>
    /// <remarks>
    /// The client-side controller issues <c>GET {uri}?q=…</c> as the user types into
    /// the dropdown; this implementation returns the identities whose name or e-mail
    /// contains the substring (case-insensitive). The result shape matches
    /// <see cref="WebExpress.WebApp.WebRestApi.RestApiWatcherItem"/> so the client can
    /// drop the chosen entry straight into the avatar row after a successful POST.
    /// </remarks>
    [Title("kleenestar.core:object.watcher.users.api.title")]
    [ObjectKeySegment]
    [Cache]
    public sealed class Index : IRestApi
    {
        /// <summary>
        /// Serialisation options used when emitting the candidate list. The camelCase
        /// policy mirrors the contract declared by
        /// <see cref="WebExpress.WebApp.WebRestApi.RestApiWatcherItem"/>'s
        /// <c>JsonPropertyName</c> attributes.
        /// </summary>
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Handles <c>GET {base}?q=…</c>: returns the candidate identities matching
        /// the provided substring (case-insensitive against name and e-mail). Only
        /// <see cref="IdentityState.Active"/> identities are surfaced so disabled or
        /// locked accounts do not appear in the picker.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var q = request?.GetParameter("q")?.Value ?? string.Empty;

            using var db = ModelHub.CreateDbContext();
            var identities = db.Identities
                .AsNoTracking()
                .Where(i => i.State == IdentityState.Active)
                .OrderBy(i => i.Name)
                .ToList();

            if (!string.IsNullOrWhiteSpace(q))
            {
                identities = [.. identities.Where(i =>
                    (i.Name ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (i.Email ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase))];
            }

            var payload = identities
                .Select(Watchers._objectkey_.Index.ToWatcherItem)
                .ToList();

            var json = JsonSerializer.Serialize(payload, _jsonOptions);

            return new ResponseOK
            {
                Content = Encoding.UTF8.GetBytes(json)
            }
                .AddHeaderContentType("application/json");
        }
    }
}
