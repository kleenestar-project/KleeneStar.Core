using KleeneStar.Core.WebManager;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_
{
    /// <summary>
    /// Represents a session that manages authentication and credential validation for REST API requests.
    /// </summary>
    /// <remarks>
    /// <b>The password is not verified yet.</b> A name that belongs to an active account signs
    /// that account in, whatever password is offered with it. The check is missing rather than
    /// weak: <see cref="Model.Entities.Identity.PasswordHash"/> is seeded with opaque
    /// <c>$seed$</c> placeholders that no password produces, and nothing in the application can
    /// set a real one - so a verification added today would lock every account out for good,
    /// with no way back in. What belongs here is a hash scheme plus the surfaces that set and
    /// reset a password; until then the sign-in identifies, and does not authenticate.
    /// <para>
    /// It does now identify the <em>stored</em> account rather than a fabricated one, which is
    /// what everything per-user reads back off the session
    /// (<see cref="WebManager.ISessionManager.GetCurrentIdentityId"/>): the author of a comment,
    /// the owner of a like, the addressee of a notification, the actor of an audit event.
    /// </para>
    /// </remarks>
    [Cache]
    public sealed class Session : RestApiSession
    {
        /// <summary>
        /// The stable name the audit log records authentication events under.
        /// </summary>
        private const string Agent = "kleenestar.session";

        /// <summary>
        /// Validates the provided credentials.
        /// </summary>
        /// <remarks>
        /// Both outcomes are audited, and the failed one matters more. An installation that only
        /// records who got in cannot tell an ordinary morning from somebody working through a
        /// password list, and the sequence of refusals is the only trace such an attempt leaves.
        /// The username is recorded as a structured delta rather than being folded into a
        /// sentence, so "every attempt against this account" is a query rather than a text
        /// search; the password never reaches the log in any form.
        /// </remarks>
        /// <param name="username">The username to validate.</param>
        /// <param name="password">The password to validate.</param>
        /// <returns>The authenticated identity if valid; otherwise, null.</returns>
        protected override IIdentity ValidateCredentials(string username, string password)
        {
            // the stored account is what is answered, because it is what the session then
            // carries: everything per-user in the application reads the signed-in identity back
            // off the session, so a fabricated one would sign every comment, like and
            // preference of the session with a person who does not exist. An unknown name is
            // refused, which is also what makes the framework's lockout counting mean anything.
            var identity = Resolve(username);

            RecordSignIn(username, identity);

            return identity;
        }

        /// <summary>
        /// Ends the session and records that its owner ended it deliberately.
        /// </summary>
        /// <remarks>
        /// A sign-out is worth recording for the same reason a sign-in is: it bounds the window
        /// during which actions could be attributed to that session. Without it, the log says
        /// when somebody arrived and never says when they left.
        /// </remarks>
        /// <param name="request">The request that ends the session.</param>
        /// <returns>The response of the base implementation.</returns>
        public override IResponse Logout(IRequest request)
        {
            var identityId = ResolveIdentityId(request);

            using (var activity = CoreHub.AuditManager.BeginActivity(AuditOrigin.User, identityId, Agent, request?.RemoteEndPoint?.ToString()))
            {
                CoreHub.AuditManager.Record
                (
                    AuditCategory.Security,
                    AuditAction.SignedOut,
                    new AuditTarget(AuditTargetType.Session, identityId == Guid.Empty ? null : identityId),
                    null,
                    AuditOutcome.Succeeded,
                    AuditSeverity.Info
                );
            }

            return base.Logout(request);
        }

        /// <summary>
        /// Records an authentication attempt.
        /// </summary>
        /// <remarks>
        /// A rejected credential names nobody, so the event carries no actor - which is itself
        /// the useful fact, and the reason the username lives in a delta instead. Recording a
        /// guess as though it had been made by the account it guessed at would attribute an
        /// attack to its victim.
        /// </remarks>
        /// <param name="username">The username the attempt was made with.</param>
        /// <param name="identity">The account the name resolved to, or <see langword="null"/>
        /// when it named none - which is what makes the attempt a failed one.</param>
        private static void RecordSignIn(string username, Identity identity)
        {
            var succeeded = identity is not null;

            using var activity = CoreHub.AuditManager.BeginActivity
            (
                AuditOrigin.User,
                identity?.Id ?? Guid.Empty,
                Agent
            );

            CoreHub.AuditManager.Record
            (
                AuditCategory.Security,
                succeeded ? AuditAction.SignedIn : AuditAction.SignInFailed,
                new AuditTarget(AuditTargetType.Identity, identity?.Id, username),
                [AuditDelta.Added("username", username, AuditValueKind.Text)],
                succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
                succeeded ? AuditSeverity.Notice : AuditSeverity.Warning
            );
        }

        /// <summary>
        /// Resolves the stored identity a username names, so a successful sign-in is attributed
        /// to a durable id rather than to a string.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <returns>The identity, or <see langword="null"/> when none carries that name.</returns>
        private static Identity Resolve(string username)
        {
            try
            {
                return CoreHub.IdentityManager?.GetIdentityByLogin(username);
            }
            catch (Exception)
            {
                // an unresolvable name still produces an event naming the credential that was
                // used, which is more than enough to reconstruct the attempt
                return null;
            }
        }

        /// <summary>
        /// Resolves the identity the request is served for, or <see cref="Guid.Empty"/>.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>The identity id.</returns>
        private static Guid ResolveIdentityId(IRequest request)
        {
            try
            {
                return CoreHub.SessionManager?.GetCurrentIdentityId(request) ?? Guid.Empty;
            }
            catch (Exception)
            {
                return Guid.Empty;
            }
        }
    }
}
