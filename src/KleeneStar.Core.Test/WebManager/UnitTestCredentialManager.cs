using KleeneStar.Core.WebIdentity;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model;
using KleeneStar.Model.Entities;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="CredentialManager"/>: the password check of the
    /// sign-in, the difference between internal and external accounts, the password change,
    /// the reset links and the link between a stored account and an external source.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestCredentialManager
    {
        private const string Secret = "correct horse battery";

        /// <summary>
        /// A stand-in for an external source: an identity provider the browser is sent to
        /// (<see cref="AcceptsPassword"/> false) or a directory that checks passwords itself.
        /// </summary>
        private sealed class ExternalSource(string key, bool acceptsPassword, bool provisions) : IAuthenticationSource
        {
            public string Key => key;
            public string Name => key;
            public int Order => 100;
            public bool IsExternal => true;
            public bool AcceptsPassword => acceptsPassword;
            public bool Provisions => provisions;
            public bool VerifyPassword(Identity account, string password) => password == "directory-password";
        }

        /// <summary>
        /// Initializes the in-memory database and CoreHub for a single test case.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);
        }

        /// <summary>
        /// Adds an internal account carrying the supplied password.
        /// </summary>
        private static Identity AddInternal(string userName, string password = Secret, IdentityState state = IdentityState.Active)
        {
            var identity = new Identity
            {
                Name = userName,
                UserName = userName,
                Email = $"{userName}@kleenestar.test",
                State = state
            };

            CoreHub.IdentityManager.Add(identity);

            if (password is not null)
            {
                ModelHub.SetPasswordHash(identity.Id, IdentityPassword.Hash(identity, password), DateTime.UtcNow);
            }

            return CoreHub.IdentityManager.GetIdentity(identity.Id);
        }

        /// <summary>
        /// Adds an account filed under an external source.
        /// </summary>
        private static Identity AddExternal(string userName, string source, string subject = null, string email = null)
        {
            var identity = new Identity
            {
                Name = userName,
                UserName = userName,
                Email = email ?? $"{userName}@kleenestar.test",
                State = IdentityState.Active,
                AuthenticationSource = source
            };

            CoreHub.IdentityManager.Add(identity);

            if (subject is not null)
            {
                ModelHub.SetExternalSubject(identity.Id, subject);
            }

            return CoreHub.IdentityManager.GetIdentity(identity.Id);
        }

        /// <summary>
        /// Verifies that the internal account signs in with its password, by user name and by
        /// e-mail address, and with nothing else.
        /// </summary>
        [Fact]
        public void Authenticate_Internal_ChecksThePassword()
        {
            Seed(nameof(Authenticate_Internal_ChecksThePassword));

            var alice = AddInternal("alice");

            Assert.Equal(alice.Id, CoreHub.CredentialManager.Authenticate("alice", Secret)?.Id);
            Assert.Equal(alice.Id, CoreHub.CredentialManager.Authenticate("ALICE@kleenestar.test", Secret)?.Id);
            Assert.Null(CoreHub.CredentialManager.Authenticate("alice", "wrong password"));
            Assert.Null(CoreHub.CredentialManager.Authenticate("alice", ""));
            Assert.Null(CoreHub.CredentialManager.Authenticate("nobody", Secret));
        }

        /// <summary>
        /// Verifies that an account without a password, one with a seeder placeholder and an
        /// inactive one do not sign in.
        /// </summary>
        [Fact]
        public void Authenticate_RefusesAccountsWithoutAUsablePassword()
        {
            Seed(nameof(Authenticate_RefusesAccountsWithoutAUsablePassword));

            AddInternal("nopassword", password: null);
            var placeholder = AddInternal("placeholder", password: null);
            ModelHub.SetPasswordHash(placeholder.Id, "$seed$v1$0123", null);
            AddInternal("locked", state: IdentityState.Locked);

            Assert.Null(CoreHub.CredentialManager.Authenticate("nopassword", Secret));
            Assert.Null(CoreHub.CredentialManager.Authenticate("placeholder", "$seed$v1$0123"));
            Assert.Null(CoreHub.CredentialManager.Authenticate("locked", Secret));
        }

        /// <summary>
        /// Verifies that an account of an identity provider is refused at the password form even
        /// when a hash is stored on it, and that a directory source checks the password itself.
        /// </summary>
        [Fact]
        public void Authenticate_External_AsksTheSource()
        {
            Seed(nameof(Authenticate_External_AsksTheSource));

            AuthenticationSourceCatalog.Register(new ExternalSource("test-idp", acceptsPassword: false, provisions: false));
            AuthenticationSourceCatalog.Register(new ExternalSource("test-ldap", acceptsPassword: true, provisions: false));

            try
            {
                var idp = AddExternal("carol", "test-idp");
                ModelHub.SetPasswordHash(idp.Id, IdentityPassword.Hash(idp, Secret), DateTime.UtcNow);
                var ldap = AddExternal("dave", "test-ldap");

                Assert.Null(CoreHub.CredentialManager.Authenticate("carol", Secret));
                Assert.Equal(ldap.Id, CoreHub.CredentialManager.Authenticate("dave", "directory-password")?.Id);
                Assert.Null(CoreHub.CredentialManager.Authenticate("dave", Secret));
            }
            finally
            {
                AuthenticationSourceCatalog.Unregister("test-idp");
                AuthenticationSourceCatalog.Unregister("test-ldap");
            }
        }

        /// <summary>
        /// Verifies that an account whose source is not installed signs in by no means - it does
        /// not fall back to the internal password check.
        /// </summary>
        [Fact]
        public void Authenticate_UninstalledSource_SignsNobodyIn()
        {
            Seed(nameof(Authenticate_UninstalledSource_SignsNobodyIn));

            var orphan = AddExternal("erin", "gone-plugin");
            ModelHub.SetPasswordHash(orphan.Id, IdentityPassword.Hash(orphan, Secret), DateTime.UtcNow);

            Assert.Null(CoreHub.CredentialManager.Authenticate("erin", Secret));
            Assert.False(AuthenticationSourceCatalog.ManagesPassword(orphan));
        }

        /// <summary>
        /// Verifies the password change: the current password is required, the rules apply, and
        /// afterwards only the new password signs in.
        /// </summary>
        [Fact]
        public void ChangePassword_ProvesTheOwnerAndAppliesTheRules()
        {
            Seed(nameof(ChangePassword_ProvesTheOwnerAndAppliesTheRules));

            var alice = AddInternal("alice");
            Identity changed = null;
            CoreHub.CredentialManager.PasswordChanged += (_, x) => changed = x;

            Assert.Equal(CredentialOutcome.WrongPassword, CoreHub.CredentialManager.ChangePassword(alice.Id, "wrong", "a new passphrase").Outcome);
            Assert.Equal(CredentialOutcome.WeakPassword, CoreHub.CredentialManager.ChangePassword(alice.Id, Secret, "short").Outcome);
            Assert.Equal(CredentialOutcome.WeakPassword, CoreHub.CredentialManager.ChangePassword(alice.Id, Secret, "alice@kleenestar.test").Outcome);
            Assert.Null(changed);

            Assert.True(CoreHub.CredentialManager.ChangePassword(alice.Id, Secret, "a new passphrase").Succeeded);

            Assert.Equal(alice.Id, changed?.Id);
            Assert.Null(CoreHub.CredentialManager.Authenticate("alice", Secret));
            Assert.Equal(alice.Id, CoreHub.CredentialManager.Authenticate("alice", "a new passphrase")?.Id);
            Assert.NotNull(CoreHub.IdentityManager.GetIdentity(alice.Id).PasswordChanged);
        }

        /// <summary>
        /// Verifies that no password is changed, and no reset link issued, for an external account.
        /// </summary>
        [Fact]
        public void PasswordOperations_RefuseExternalAccounts()
        {
            Seed(nameof(PasswordOperations_RefuseExternalAccounts));

            AuthenticationSourceCatalog.Register(new ExternalSource("test-idp", acceptsPassword: false, provisions: false));

            try
            {
                var carol = AddExternal("carol", "test-idp");

                Assert.Equal(CredentialOutcome.ExternalAccount, CoreHub.CredentialManager.ChangePassword(carol.Id, Secret, "a new passphrase").Outcome);
                Assert.Equal(CredentialOutcome.ExternalAccount, CoreHub.CredentialManager.IssuePasswordReset(carol.Id, Guid.Empty, out var secret).Outcome);
                Assert.Null(secret);
            }
            finally
            {
                AuthenticationSourceCatalog.Unregister("test-idp");
            }
        }

        /// <summary>
        /// Verifies the reset link: it sets the password once, a later link supersedes an earlier
        /// one, and its secret is not what is stored.
        /// </summary>
        [Fact]
        public void PasswordReset_SetsThePasswordOnce()
        {
            Seed(nameof(PasswordReset_SetsThePasswordOnce));

            var bob = AddInternal("bob", password: null);

            var first = CoreHub.CredentialManager.IssuePasswordReset(bob.Id, Guid.Empty, out var firstSecret);
            var second = CoreHub.CredentialManager.IssuePasswordReset(bob.Id, Guid.Empty, out var secondSecret);

            Assert.True(first.Succeeded);
            Assert.True(second.Succeeded);
            Assert.NotEqual(secondSecret, second.Reset.TokenHash);
            Assert.Null(CoreHub.CredentialManager.GetPasswordReset(firstSecret));
            Assert.NotNull(CoreHub.CredentialManager.GetPasswordReset(secondSecret));

            Assert.Equal(CredentialOutcome.InvalidLink, CoreHub.CredentialManager.CompletePasswordReset(firstSecret, "a new passphrase").Outcome);
            Assert.Equal(CredentialOutcome.WeakPassword, CoreHub.CredentialManager.CompletePasswordReset(secondSecret, "short").Outcome);
            Assert.True(CoreHub.CredentialManager.CompletePasswordReset(secondSecret, "a new passphrase").Succeeded);
            Assert.Equal(CredentialOutcome.InvalidLink, CoreHub.CredentialManager.CompletePasswordReset(secondSecret, "another passphrase").Outcome);

            Assert.Equal(bob.Id, CoreHub.CredentialManager.Authenticate("bob", "a new passphrase")?.Id);
        }

        /// <summary>
        /// Verifies that an external sign-in finds its account by source and subject, never an
        /// internal account by name or e-mail.
        /// </summary>
        [Fact]
        public void SignInExternal_MatchesSubjectNotName()
        {
            Seed(nameof(SignInExternal_MatchesSubjectNotName));

            AuthenticationSourceCatalog.Register(new ExternalSource("test-idp", acceptsPassword: false, provisions: false));

            try
            {
                var admin = AddInternal("admin");
                var linked = AddExternal("carol", "test-idp", subject: "sub-carol");

                var byName = CoreHub.CredentialManager.SignInExternal("test-idp", new ExternalAccountClaims("sub-x", "admin", "Admin", admin.Email, true));
                var bySubject = CoreHub.CredentialManager.SignInExternal("test-idp", new ExternalAccountClaims("sub-carol", "other", "Other", "other@x", false));

                Assert.Null(byName);
                Assert.Equal(linked.Id, bySubject?.Id);
                Assert.Null(CoreHub.IdentityManager.GetIdentity(admin.Id).ExternalSubject);
            }
            finally
            {
                AuthenticationSourceCatalog.Unregister("test-idp");
            }
        }

        /// <summary>
        /// Verifies that an account an administrator prepared for a source is claimed by the
        /// first sign-in whose verified e-mail address is its own - and only by a verified one.
        /// </summary>
        [Fact]
        public void SignInExternal_ClaimsAPreparedAccountByVerifiedEmail()
        {
            Seed(nameof(SignInExternal_ClaimsAPreparedAccountByVerifiedEmail));

            AuthenticationSourceCatalog.Register(new ExternalSource("test-idp", acceptsPassword: false, provisions: false));

            try
            {
                var prepared = AddExternal("frank", "test-idp", email: "frank@corp.test");

                Assert.Null(CoreHub.CredentialManager.SignInExternal("test-idp", new ExternalAccountClaims("sub-frank", "frank", "Frank", "frank@corp.test", false)));

                var claimed = CoreHub.CredentialManager.SignInExternal("test-idp", new ExternalAccountClaims("sub-frank", "frank", "Frank", "FRANK@corp.test", true));

                Assert.Equal(prepared.Id, claimed?.Id);
                Assert.Equal("sub-frank", CoreHub.IdentityManager.GetIdentity(prepared.Id).ExternalSubject);
                Assert.Equal(prepared.Id, CoreHub.CredentialManager.SignInExternal("test-idp", new ExternalAccountClaims("sub-frank", null, null, null, false))?.Id);
            }
            finally
            {
                AuthenticationSourceCatalog.Unregister("test-idp");
            }
        }

        /// <summary>
        /// Verifies that a provisioning source creates the account on its first sign-in, with a
        /// login name no other account carries, and finds it again on the next.
        /// </summary>
        [Fact]
        public void SignInExternal_ProvisionsWhereTheSourceSaysSo()
        {
            Seed(nameof(SignInExternal_ProvisionsWhereTheSourceSaysSo));

            AuthenticationSourceCatalog.Register(new ExternalSource("test-sso", acceptsPassword: false, provisions: true));

            try
            {
                AddInternal("grace");

                var created = CoreHub.CredentialManager.SignInExternal("test-sso", new ExternalAccountClaims("sub-grace", "Grace", "Grace Hopper", "grace@corp.test", true));

                Assert.NotNull(created);
                Assert.Equal("test-sso", created.AuthenticationSource);
                Assert.Equal("sub-grace", created.ExternalSubject);
                Assert.Equal("grace2", created.UserName);
                Assert.Null(created.PasswordHash);
                Assert.Equal(created.Id, CoreHub.CredentialManager.SignInExternal("test-sso", new ExternalAccountClaims("sub-grace", null, null, null, false))?.Id);
            }
            finally
            {
                AuthenticationSourceCatalog.Unregister("test-sso");
            }
        }

        /// <summary>
        /// Verifies that an ordinary update of an account never writes its credentials, whatever
        /// the incoming copy carries - the copies that went out to a client come back blanked.
        /// </summary>
        [Fact]
        public void IdentityUpdate_LeavesTheCredentialsAlone()
        {
            Seed(nameof(IdentityUpdate_LeavesTheCredentialsAlone));

            var alice = AddInternal("alice");

            alice.PasswordHash = null;
            alice.Name = "Alice Renamed";
            CoreHub.IdentityManager.Update(alice);

            Assert.Equal("Alice Renamed", CoreHub.IdentityManager.GetIdentity(alice.Id).Name);
            Assert.Equal(alice.Id, CoreHub.CredentialManager.Authenticate("alice", Secret)?.Id);
        }

        /// <summary>
        /// Verifies that moving an account to another source drops the password it had, so it
        /// no longer signs in with it.
        /// </summary>
        [Fact]
        public void IdentityUpdate_SourceSwitchDropsThePassword()
        {
            Seed(nameof(IdentityUpdate_SourceSwitchDropsThePassword));

            AuthenticationSourceCatalog.Register(new ExternalSource("test-idp", acceptsPassword: false, provisions: false));

            try
            {
                var alice = AddInternal("alice");

                alice.AuthenticationSource = "test-idp";
                CoreHub.IdentityManager.Update(alice);

                var moved = CoreHub.IdentityManager.GetIdentity(alice.Id);
                Assert.Equal("test-idp", moved.AuthenticationSource);
                Assert.Null(moved.PasswordHash);

                moved.AuthenticationSource = IdentitySource.Local;
                CoreHub.IdentityManager.Update(moved);

                Assert.Null(CoreHub.IdentityManager.GetIdentity(alice.Id).AuthenticationSource);
                Assert.Null(CoreHub.CredentialManager.Authenticate("alice", Secret));
            }
            finally
            {
                AuthenticationSourceCatalog.Unregister("test-idp");
            }
        }

        /// <summary>
        /// Verifies that the account administrators are the active members of the
        /// administrators group, and nobody else.
        /// </summary>
        [Fact]
        public void AccountAuthorization_IsTheAdministratorsGroup()
        {
            var connectionString = nameof(AccountAuthorization_IsTheAdministratorsGroup);
            Seed(connectionString);

            var memberId = Guid.NewGuid();
            var lockedId = Guid.NewGuid();
            var outsiderId = Guid.NewGuid();

            using (var db = CoreHubFixture.CreateDbContext(connectionString))
            {
                var admins = new Group { Id = Group.AdministratorsId, Name = "Admin" };
                var others = new Group { Id = Guid.NewGuid(), Name = "Others" };
                db.Groups.AddRange(admins, others);
                db.Identities.Add(new Identity { Id = memberId, Name = "M", UserName = "m", Email = "m@x", GroupMemberships = [new IdentityGroupMembership { Group = admins }] });
                db.Identities.Add(new Identity { Id = lockedId, Name = "L", UserName = "l", Email = "l@x", State = IdentityState.Locked, GroupMemberships = [new IdentityGroupMembership { Group = admins }] });
                db.Identities.Add(new Identity { Id = outsiderId, Name = "O", UserName = "o", Email = "o@x", GroupMemberships = [new IdentityGroupMembership { Group = others }] });
                db.SaveChanges();
            }

            Assert.True(AccountAuthorization.IsAdministrator(memberId));
            Assert.False(AccountAuthorization.IsAdministrator(lockedId));
            Assert.False(AccountAuthorization.IsAdministrator(outsiderId));
            Assert.False(AccountAuthorization.IsAdministrator(Guid.Empty));
        }
    }
}
