using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using WebExpress.WebCore;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages the devices and browsers that are currently signed in with an identity, as
    /// listed on the profile's "active sessions" page.
    /// </summary>
    /// <remarks>
    /// A session is a WebExpress sign-in <em>grant</em>. The framework keeps no list of them -
    /// it signs tokens and remembers only what it revoked - so the list is recorded here, the
    /// first time a request carries a grant (<see cref="Accepts"/>), whichever way the account
    /// signed in. Ending a session revokes its grant in the framework's token store: the refresh
    /// token dies there, and <see cref="Accepts"/> refuses the access token that is still in
    /// the browser from the next request on.
    /// <para>
    /// The session the page is being served to is marked <see cref="IdentitySession.Current"/>
    /// per request and is never ended from here, so a user cannot lock themselves out of the
    /// very page they are looking at - signing out is what ends it.
    /// </para>
    /// </remarks>
    public sealed class IdentitySessionManager : IIdentitySessionManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// How often a session's last activity is written at most. Every request of a signed-in
        /// user passes here, and a row write per request would be a write per click.
        /// </summary>
        private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// When each grant's row was last written by this process.
        /// </summary>
        private static readonly ConcurrentDictionary<string, DateTime> _touched = new(StringComparer.Ordinal);

        /// <summary>
        /// An event that fires when a session is ended.
        /// </summary>
        public event EventHandler<IdentitySession> IdentitySessionRemoved;

        /// <summary>
        /// Initializes a new instance of the class. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private IdentitySessionManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns the sessions of the identity the request is served for, the current device
        /// first.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>An enumerable collection of sessions (possibly empty).</returns>
        public IEnumerable<IdentitySession> GetSessions(IRequest request)
        {
            var currentGrant = CoreHub.SessionManager.GetCurrentCredential(request)?.GrantId;

            return [.. GetSessions(CoreHub.SessionManager.GetCurrentIdentityId(request))
                .Select(x =>
                {
                    x.Current = currentGrant is not null && x.GrantId == currentGrant;
                    return x;
                })
                .OrderByDescending(x => x.Current)
                .ThenByDescending(x => x.LastActive)];
        }

        /// <summary>
        /// Returns the sessions of the given identity whose grant has not ended.
        /// </summary>
        /// <param name="ownerId">The signed-in identity.</param>
        /// <returns>An enumerable collection of sessions (possibly empty).</returns>
        public IEnumerable<IdentitySession> GetSessions(Guid ownerId)
        {
            return ModelHub.GetIdentitySessions(ownerId);
        }

        /// <summary>
        /// Returns a session by its id.
        /// </summary>
        /// <param name="sessionId">The id of the session.</param>
        /// <returns>The session, or <see langword="null"/> when no such session exists.</returns>
        public IdentitySession GetSession(Guid sessionId)
        {
            return ModelHub.GetIdentitySession(sessionId);
        }

        /// <summary>
        /// Ends the session with the given id: revokes its grant and forgets it. The session of
        /// the request in flight is not ended here.
        /// </summary>
        /// <param name="sessionId">The id of the session to end.</param>
        /// <returns>The current instance for method chaining.</returns>
        public IIdentitySessionManager Revoke(Guid sessionId)
        {
            var session = GetSession(sessionId);
            var currentGrant = CoreHub.SessionManager.GetCurrentCredential(null)?.GrantId;

            if (session is null || session.GrantId == currentGrant)
            {
                return this;
            }

            RevokeGrant(session);
            ModelHub.RemoveIdentitySession(sessionId);

            IdentitySessionRemoved?.Invoke(this, session);

            CoreHub.AddNotification("kleenestar.core:notification.title.deleted", "kleenestar.core:notification.session.revoked", session);

            return this;
        }

        /// <summary>
        /// Ends every session of the identity the request is served for except the current one.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>The current instance for method chaining.</returns>
        public IIdentitySessionManager RevokeOthers(IRequest request)
        {
            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var currentGrant = CoreHub.SessionManager.GetCurrentCredential(request)?.GrantId;

            // without knowing which grant is the caller's own, "all others" would include it
            if (ownerId == Guid.Empty || currentGrant is null)
            {
                return this;
            }

            var removed = ModelHub.RemoveOtherIdentitySessions(ownerId, currentGrant);

            if (removed.Count == 0)
            {
                return this;
            }

            foreach (var session in removed)
            {
                RevokeGrant(session);
                IdentitySessionRemoved?.Invoke(this, session);
            }

            // no single session left to name, so the entry points at the list the sessions were
            // ended from rather than carrying no link at all
            CoreHub.AddNotification
            (
                "kleenestar.core:notification.title.deleted",
                "kleenestar.core:notification.session.revokedall",
                5000,
                null,
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Profile.Sessions.Index>()?.ToString()
            );

            return this;
        }

        /// <summary>
        /// Decides whether a sign-in may still act, and records that it did.
        /// </summary>
        /// <param name="credential">The verified sign-in credential.</param>
        /// <param name="request">The request it came with.</param>
        /// <returns><see langword="true"/> when the grant has not been revoked.</returns>
        public bool Accepts(WebIdentity.SessionCredential credential, IRequest request)
        {
            var grant = credential?.GrantId;

            // an access token always names its grant; one that does not was not issued by a
            // sign-in, and there is no session to check it against
            if (grant is null)
            {
                return credential is not null;
            }

            if (Store(credential.Application)?.IsRevoked("grant:" + grant) == true)
            {
                _touched.TryRemove(grant, out _);

                return false;
            }

            Touch(credential, request);

            return true;
        }

        /// <summary>
        /// Forgets the session of a grant that ended by signing out.
        /// </summary>
        /// <param name="grantId">The grant.</param>
        public void End(string grantId)
        {
            if (string.IsNullOrEmpty(grantId))
            {
                return;
            }

            _touched.TryRemove(grantId, out _);

            try
            {
                ModelHub.RemoveIdentitySessionByGrant(grantId);
            }
            catch (Exception ex)
            {
                // the grant is revoked by the framework either way; the row is only the listing
                _componentHub?.LogManager?.DefaultLog?.Exception(ex);
            }
        }

        /// <summary>
        /// Writes the session of a grant: the first time it is seen, and then at most every
        /// <see cref="TouchInterval"/>.
        /// </summary>
        /// <param name="credential">The sign-in credential.</param>
        /// <param name="request">The request.</param>
        private void Touch(WebIdentity.SessionCredential credential, IRequest request)
        {
            var grant = credential.GrantId;
            var now = DateTime.UtcNow;

            if (_touched.TryGetValue(grant, out var last) && now - last < TouchInterval)
            {
                return;
            }

            _touched[grant] = now;

            try
            {
                var agent = request?.Header?.UserAgent;
                var (device, client, mobile) = DescribeAgent(agent);

                ModelHub.TouchIdentitySession(new IdentitySession
                {
                    OwnerId = credential.Identity.Id,
                    GrantId = grant,
                    Expires = credential.GrantExpires ?? credential.Expires ?? now,
                    Device = device,
                    Client = client,
                    Mobile = mobile,
                    IpAddress = Mask(request?.RemoteEndPoint),
                    Created = now,
                    LastActive = now
                });

                // the list drops ended grants, and so does the table - on the way, rather
                // than by a job of its own
                ModelHub.RemoveExpiredIdentitySessions();
            }
            catch (Exception ex)
            {
                // a request must not fail because its session could not be listed
                _componentHub?.LogManager?.DefaultLog?.Exception(ex);
            }
        }

        /// <summary>
        /// Revokes the grant of a session in the framework's token store, so neither its
        /// refresh token nor its access token signs anybody in again.
        /// </summary>
        /// <param name="session">The session.</param>
        private void RevokeGrant(IdentitySession session)
        {
            if (string.IsNullOrEmpty(session?.GrantId))
            {
                return;
            }

            _touched.TryRemove(session.GrantId, out _);

            // every KleeneStar sign-in is a grant of the core application (see SessionCredential)
            var expires = session.Expires > DateTime.UtcNow ? session.Expires : DateTime.UtcNow.AddMinutes(1);

            Store(CoreHub.ApplicationContext)?.Revoke("grant:" + session.GrantId, new DateTimeOffset(expires, TimeSpan.Zero));
        }

        /// <summary>
        /// Returns the framework's token store of an application.
        /// </summary>
        /// <param name="application">The application.</param>
        /// <returns>The store, or <see langword="null"/> when none is configured.</returns>
        private IIdentityTokenStore Store(IApplicationContext application)
        {
            var hub = _componentHub ?? CoreHub.ComponentHub;

            return application is null ? null : hub?.IdentityTokenStoreManager?.GetStore(application);
        }

        /// <summary>
        /// Derives the device, the client and whether it is a handheld from a user agent. A
        /// rough reading - enough to tell one's own devices apart, not a fingerprint.
        /// </summary>
        /// <param name="agent">The user agent.</param>
        /// <returns>The device, the client and the handheld flag.</returns>
        internal static (string Device, string Client, bool Mobile) DescribeAgent(string agent)
        {
            if (string.IsNullOrWhiteSpace(agent))
            {
                return ("Unknown device", null, false);
            }

            var device = agent switch
            {
                _ when agent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) => "iPhone",
                _ when agent.Contains("iPad", StringComparison.OrdinalIgnoreCase) => "iPad",
                _ when agent.Contains("Android", StringComparison.OrdinalIgnoreCase) => "Android",
                _ when agent.Contains("Windows", StringComparison.OrdinalIgnoreCase) => "Windows",
                _ when agent.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase) || agent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase) => "macOS",
                _ when agent.Contains("Linux", StringComparison.OrdinalIgnoreCase) => "Linux",
                _ => "Unknown device"
            };

            var client = agent switch
            {
                _ when agent.Contains("Edg/", StringComparison.Ordinal) => "Edge",
                _ when agent.Contains("OPR/", StringComparison.Ordinal) => "Opera",
                _ when agent.Contains("Firefox/", StringComparison.Ordinal) => "Firefox",
                _ when agent.Contains("Chrome/", StringComparison.Ordinal) => "Chrome",
                _ when agent.Contains("Safari/", StringComparison.Ordinal) => "Safari",
                _ when agent.StartsWith("curl/", StringComparison.OrdinalIgnoreCase) => "curl",
                _ => agent.Length > 64 ? agent[..64] : agent
            };

            var mobile = agent.Contains("Mobi", StringComparison.OrdinalIgnoreCase)
                || device is "iPhone" or "Android";

            return (device, client, mobile);
        }

        /// <summary>
        /// Masks a remote address so the list never discloses a full one: the third part of an
        /// IPv4 address, everything after the third group of an IPv6 address.
        /// </summary>
        /// <param name="endPoint">The remote end point.</param>
        /// <returns>The masked address, or <see langword="null"/>.</returns>
        internal static string Mask(EndPoint endPoint)
        {
            var address = (endPoint as IPEndPoint)?.Address;

            if (address is null)
            {
                return null;
            }

            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var parts = address.ToString().Split('.');

                return $"{parts[0]}.{parts[1]}.···.{parts[3]}";
            }

            var groups = address.ToString().Split(':');

            return string.Join(':', groups.Take(3)) + ":···";
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
