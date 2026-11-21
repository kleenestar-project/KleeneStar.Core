using KleeneStar.Core.WebIdentity;
using KleeneStar.Core.WebManager;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.Test.WebIdentity
{
    /// <summary>
    /// Tests the identity validator the framework asks on every authenticated request
    /// (<see cref="SessionValidatorBase"/>) and the credential it judges.
    /// </summary>
    public class UnitTestSessionValidator
    {
        /// <summary>
        /// The framework discovers only public sealed validators.
        /// </summary>
        [Fact]
        public void ValidatorIsDiscoverable()
        {
            var type = typeof(SessionValidator);

            Assert.True(type.IsPublic && type.IsSealed);
            Assert.True(typeof(IIdentityValidator).IsAssignableFrom(type));
            Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
        }

        /// <summary>
        /// A credential without an identity is never accepted.
        /// </summary>
        [Fact]
        public void CredentialWithoutIdentityIsRefused()
        {
            var manager = (SessionManager)Activator.CreateInstance
            (
                typeof(SessionManager),
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                [null, null],
                null
            );

            Assert.False(manager.Accepts(null, null));
            Assert.Null(SessionCredential.From(null));
            Assert.Null(SessionCredential.From(new IdentityValidationContext()));
        }

        /// <summary>
        /// The grant and the token use come from the validation context: a sign-in carries the
        /// context's grant, a personal access token carries none.
        /// </summary>
        [Fact]
        public void CredentialTakesGrantAndUseFromTheContext()
        {
            var identity = new TestIdentity();

            var signIn = SessionCredential.From(new IdentityValidationContext { Identity = identity, Grant = "g1", TokenUse = "access" });
            var personal = SessionCredential.From(new IdentityValidationContext { Identity = identity, Grant = "g1", TokenUse = "pat" });

            Assert.False(signIn.Personal);
            Assert.Equal("g1", signIn.GrantId);
            Assert.True(personal.Personal);
            Assert.Null(personal.GrantId);
        }

        /// <summary>
        /// An identity the stand-in tests hand to the credential.
        /// </summary>
        private sealed class TestIdentity : IIdentity
        {
            public Guid Id { get; } = Guid.NewGuid();
            public string Name => "test";
            public string Email => null;
            public string PasswordHash => null;
            public IEnumerable<IIdentityGroup> Groups => [];
        }
    }
}
