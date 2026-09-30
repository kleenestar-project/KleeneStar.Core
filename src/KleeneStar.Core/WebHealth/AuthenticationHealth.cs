using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebHealt;

namespace KleeneStar.Core.WebHealth
{
    /// <summary>
    /// Fails the health probe when the application cannot issue sign-in tokens.
    /// </summary>
    /// <remarks>
    /// Without a usable <c>WebExpress:Authentication</c> section every sign-in fails - the framework
    /// refuses to invent a signing key per process - while every anonymous page still answers. An
    /// instance in that state looks alive to anything that does not try to sign in, which is how a
    /// container whose secret was never mounted ends up receiving traffic. The question is the
    /// framework's own (<c>IIdentityManager.IsAuthenticationConfigured</c>), so the check follows
    /// every rule a sign-in enforces; the framework writes the offending setting to the server log,
    /// never the key.
    /// </remarks>
    [HealthTimeout(1000)]
    public sealed class AuthenticationHealth : IHealth
    {
        /// <summary>
        /// The configuration section the framework reads the sign-in settings from.
        /// </summary>
        public const string Section = "WebExpress:Authentication";

        private readonly IHealthContext _healthContext;
        private readonly IComponentHub _componentHub;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="healthContext">The binding, whose application the tokens are issued for.</param>
        /// <param name="componentHub">The framework, whose identity manager is asked.</param>
        private AuthenticationHealth(IHealthContext healthContext, IComponentHub componentHub)
        {
            _healthContext = healthContext;
            _componentHub = componentHub;
        }

        /// <summary>
        /// Asks the framework whether the bound application can issue sign-in tokens.
        /// </summary>
        /// <param name="cancellationToken">The budget of the check.</param>
        /// <returns>The outcome; the framework has already logged why a present section is unusable.</returns>
        public Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
        {
            var configured = _componentHub?.IdentityManager?
                .IsAuthenticationConfigured(_healthContext?.ApplicationContext) == true;

            return Task.FromResult(configured
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"The section {Section} is missing or unusable, or no token store is available."));
        }
    }
}
