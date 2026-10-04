using KleeneStar.Core.WebHealth;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Reflection;
using WebExpress.WebCore;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebHealth;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebPlugin;

namespace KleeneStar.Core.Test.WebHealth
{
    /// <summary>
    /// Provides unit tests for <see cref="AuthenticationHealth"/>: a host whose sign-in
    /// configuration cannot issue a token is unhealthy.
    /// </summary>
    /// <remarks>
    /// The check asks the framework instead of restating its rules, so the tests run it against
    /// a real component hub: a rule the framework adds or tightens shows up here without a change.
    /// </remarks>
    [Collection("NonParallelTests")]
    public class UnitTestAuthenticationHealth : IDisposable
    {
        /// <summary>
        /// A 256-bit key, the smallest the framework accepts.
        /// </summary>
        private static readonly string ValidKey = Convert.ToBase64String(new byte[32]);

        /// <summary>
        /// The token store directory of the test, which the framework creates on demand.
        /// </summary>
        private readonly string _tokenStorePath = Path.Combine(Path.GetTempPath(), "kleenestar-auth-" + Guid.NewGuid().ToString("N"));

        /// <summary>
        /// Removes the token store directory the framework created.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(_tokenStorePath))
            {
                Directory.Delete(_tokenStorePath, true);
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Tests that the settings the host ships with are healthy.
        /// </summary>
        [Fact]
        public async Task CompleteSectionIsHealthy()
        {
            var (result, _) = await Check(Configuration());

            Assert.True(result.IsHealthy);
        }

        /// <summary>
        /// Tests that a host without the section - every sign-in would throw - is unhealthy.
        /// </summary>
        [Fact]
        public async Task MissingSectionIsUnhealthy()
        {
            var (result, _) = await Check(new ConfigurationBuilder().Build());

            Assert.False(result.IsHealthy);
            Assert.Contains(AuthenticationHealth.Section, result.Description);
        }

        /// <summary>
        /// Tests that a check activated without the framework is unhealthy rather than failing.
        /// </summary>
        [Fact]
        public async Task NoFrameworkIsUnhealthy()
        {
            var result = await Create(null, null).CheckAsync(TestContext.Current.CancellationToken);

            Assert.False(result.IsHealthy);
        }

        /// <summary>
        /// Tests that each setting the framework requires fails the check on its own, and that
        /// neither the diagnostic nor the server log ever carries the key.
        /// </summary>
        /// <param name="key">The setting to override.</param>
        /// <param name="value">The value it is overridden with.</param>
        [Theory]
        [InlineData("Issuer", "")]
        [InlineData("Audience", " ")]
        [InlineData("SigningKey", "")]
        [InlineData("SigningKey", "not base64!")]
        [InlineData("SigningKey", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==")]
        [InlineData("AccessTokenLifetime", "00:00:00")]
        [InlineData("RefreshTokenLifetime", "00:30:00")]
        [InlineData("TokenStorePath", "")]
        public async Task IncompleteSectionIsUnhealthy(string key, string value)
        {
            var (result, log) = await Check(Configuration((key, value)));

            Assert.False(result.IsHealthy);
            Assert.DoesNotContain(ValidKey, result.Description);
            Assert.DoesNotContain(log.GetRecentEntries(), x => x.Message.Contains(ValidKey));
        }

        /// <summary>
        /// Builds the authentication section as the shipped webexpress.settings.json carries it,
        /// with the given values overridden.
        /// </summary>
        /// <param name="overrides">The settings to override.</param>
        /// <returns>The configuration.</returns>
        private IConfigurationRoot Configuration(params (string Key, string Value)[] overrides)
        {
            var values = new Dictionary<string, string>
            {
                ["Issuer"] = "kleenestar",
                ["Audience"] = "kleenestar",
                ["SigningKey"] = ValidKey,
                ["RequireHttps"] = "false",
                ["AccessTokenLifetime"] = "01:00:00",
                ["TokenStorePath"] = _tokenStorePath
            };

            foreach (var (key, value) in overrides)
            {
                values[key] = value;
            }

            return new ConfigurationBuilder()
                .AddInMemoryCollection(values.ToDictionary(x => $"{AuthenticationHealth.Section}:{x.Key}", x => x.Value))
                .Build();
        }

        /// <summary>
        /// Runs the check against a component hub that carries the given configuration.
        /// </summary>
        /// <param name="configuration">The merged configuration of the host.</param>
        /// <returns>The outcome and the server log the framework wrote its reason to.</returns>
        private static async Task<(HealthCheckResult Result, ILog Log)> Check(IConfigurationRoot configuration)
        {
            var log = new Log { LogMode = LogMode.Off };
            var server = new HttpServerContext
            (
                new RouteEndpoint("server"),
                [],
                Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString()),
                Environment.CurrentDirectory,
                Environment.CurrentDirectory,
                Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString()),
                configuration,
                CultureInfo.GetCultureInfo("en"),
                log,
                null
            );
            using var hub = (ComponentHub)typeof(ComponentHub)
                .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, [typeof(IHttpServerContext)])
                .Invoke([server]);

            var result = await Create(new Binding(), hub).CheckAsync(TestContext.Current.CancellationToken);

            return (result, log);
        }

        /// <summary>
        /// Creates the check through its private constructor, the way the framework does.
        /// </summary>
        /// <param name="healthContext">The binding to inject.</param>
        /// <param name="componentHub">The framework to inject.</param>
        /// <returns>The check.</returns>
        private static IHealth Create(IHealthContext healthContext, IComponentHub componentHub)
        {
            var constructor = typeof(AuthenticationHealth).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single();

            return (IHealth)constructor.Invoke([healthContext, componentHub]);
        }

        /// <summary>
        /// Binds the check to an application, which is all the framework's question needs.
        /// </summary>
        private sealed class Binding : IHealthContext
        {
            /// <summary>
            /// Gets no plugin; the question does not depend on one.
            /// </summary>
            public IPluginContext PluginContext => null;

            /// <summary>
            /// Gets the application the tokens would be issued for.
            /// </summary>
            public IApplicationContext ApplicationContext { get; } = new ApplicationContext();

            /// <summary>
            /// Gets no id; the check is not discovered here.
            /// </summary>
            public IComponentId HealthId => null;

            /// <summary>
            /// Gets the budget the check declares.
            /// </summary>
            public TimeSpan Timeout => TimeSpan.FromSeconds(1);
        }
    }
}
