using KleeneStar.Core.WebManager;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
// the framework carries an identity of its own in the namespace this file imports, so the
// stored one is named explicitly rather than by the name both of them carry
using IdentityEntity = KleeneStar.Model.Entities.Identity;

namespace KleeneStar.Core.WWW.Api._1_
{
    /// <summary>
    /// Represents a session that manages authentication and credential validation for REST API requests.
    /// </summary>
    /// <remarks>
    /// The password is checked by the source the account names
    /// (<see cref="WebManager.ICredentialManager.Authenticate"/>): the stored hash for an
    /// internal account, the directory for an external one that takes passwords. An account of
    /// an identity provider the browser is sent to (OpenID Connect) is refused here - its
    /// password is not this form's to receive.
    /// <para>
    /// The answer is the <em>stored</em> account rather than a fabricated one, which is what
    /// everything per-user reads back off the session
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
            // preference of the session with a person who does not exist. An unknown name and a
            // wrong password are refused alike, which is also what makes the framework's lockout
            // counting mean anything.
            var identity = Authenticate(username, password);

            RecordSignIn(username, identity);

            return identity;
        }

        /// <summary>
        /// Issues the credentials a verified sign-in is carried by, and answers a failed
        /// sign-in rather than a server error when none can be issued.
        /// </summary>
        /// <remarks>
        /// The credential is what a sign-in produces: since the framework signs tokens instead
        /// of keeping a session, an installation whose <c>WebExpress:Authentication</c> section
        /// is missing or unusable refuses every sign-in from inside the issuer, and the
        /// endpoint answered a 500 whose cause nothing named - the framework logs the
        /// reflection wrapper around the endpoint, never the exception inside it. This is a
        /// configuration fault rather than a wrong password, so it is logged with its own
        /// message and audited as a failed attempt by the account it was made for; the caller
        /// is told the sign-in failed, which is the truth of it.
        /// </remarks>
        /// <param name="identity">The account the credentials belong to.</param>
        /// <param name="request">The request the sign-in was made with.</param>
        /// <returns>The issued credentials, or null when they could not be issued.</returns>
        protected override IdentityTokenPair EstablishIdentity(IIdentity identity, IRequest request)
        {
            try
            {
                return base.EstablishIdentity(identity, request);
            }
            catch (Exception ex)
            {
                CoreHub.ComponentHub?.LogManager?.DefaultLog?.Exception(ex);

                RecordSignInFailure(identity, ex);

                return null;
            }
        }

        /// <summary>
        /// Ends the session and records that its owner ended it deliberately.
        /// </summary>
        /// <remarks>
        /// A sign-out is worth recording for the same reason a sign-in is: it bounds the window
        /// during which actions could be attributed to that session. Without it, the log says
        /// when somebody arrived and never says when they left.
        /// <para>
        /// The <c>DELETE</c> attribute has to be repeated here: the framework routes a verb to the
        /// method that declares it, and an override that does not declare it again leaves the
        /// endpoint answering "The method 'DELETE' is not supported" - the sign-out button then
        /// redirects as though it worked, and the caller stays signed in.
        /// </para>
        /// </remarks>
        /// <param name="request">The request that ends the session.</param>
        /// <returns>The response of the base implementation.</returns>
        [Method(RequestMethod.DELETE)]
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

            // the framework revokes the grant; the session list forgets it, so the device does
            // not linger on the profile as though it were still signed in
            var grant = CoreHub.SessionManager?.GetCurrentCredential(request)?.GrantId;
            var response = base.Logout(request);

            CoreHub.IdentitySessionManager?.End(grant);

            return response;
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
        private static void RecordSignIn(string username, IdentityEntity identity)
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
        /// Records a sign-in that was refused after the credentials had been accepted.
        /// </summary>
        /// <remarks>
        /// The attempt named a real account and still ended without a credential, so the event
        /// names that account and carries the issuer's reason as a delta. It is critical rather
        /// than a warning: a failed password is one person's bad morning, an installation that
        /// cannot issue credentials is everybody's.
        /// </remarks>
        /// <param name="identity">The account the sign-in was made for.</param>
        /// <param name="reason">The failure the issuer reported.</param>
        private static void RecordSignInFailure(IIdentity identity, Exception reason)
        {
            using var activity = CoreHub.AuditManager.BeginActivity
            (
                AuditOrigin.User,
                identity?.Id ?? Guid.Empty,
                Agent
            );

            CoreHub.AuditManager.Record
            (
                AuditCategory.Security,
                AuditAction.SignInFailed,
                new AuditTarget(AuditTargetType.Identity, identity?.Id, identity?.Name),
                [AuditDelta.Added("reason", reason?.Message, AuditValueKind.Text)],
                AuditOutcome.Failed,
                AuditSeverity.Critical
            );
        }

        /// <summary>
        /// Authenticates the stored account a username names, so a successful sign-in is
        /// attributed to a durable id rather than to a string.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns>The identity, or <see langword="null"/> when the name names none or the
        /// password does not authenticate it.</returns>
        private static IdentityEntity Authenticate(string username, string password)
        {
            try
            {
                return CoreHub.CredentialManager?.Authenticate(username, password);
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
