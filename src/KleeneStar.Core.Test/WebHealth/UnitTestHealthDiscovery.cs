using KleeneStar.Core.WebHealth;
using System.Reflection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebHealth;

namespace KleeneStar.Core.Test.WebHealth
{
    /// <summary>
    /// Provides unit tests for the shape of the core's health components, as the WebExpress
    /// <c>HealthManager</c> discovers them.
    /// </summary>
    /// <remarks>
    /// The manager binds only classes that are public, sealed and not generic, and it says
    /// nothing about a class it skips - a check that is not sealed is simply never run, and
    /// <c>/health</c> stays green without it. These tests keep the shipped set discoverable.
    /// </remarks>
    public class UnitTestHealthDiscovery
    {
        /// <summary>
        /// The health components the core is expected to contribute.
        /// </summary>
        public static TheoryData<Type> ShippedChecks =>
        [
            typeof(DatabaseHealth),
            typeof(AuthenticationHealth)
        ];

        /// <summary>
        /// Tests that every health component of the core assembly meets the framework's
        /// discovery rule.
        /// </summary>
        [Fact]
        public void EveryHealthComponentIsDiscoverable()
        {
            var checks = typeof(KleeneStarApplication).Assembly.GetTypes()
                .Where(x => x.IsClass && !x.IsAbstract && typeof(IHealth).IsAssignableFrom(x))
                .ToArray();

            Assert.NotEmpty(checks);
            Assert.All(checks, x =>
            {
                Assert.True(x.IsPublic, $"{x.Name} must be public to be discovered.");
                Assert.True(x.IsSealed, $"{x.Name} must be sealed to be discovered.");
                Assert.False(x.ContainsGenericParameters, $"{x.Name} must not be generic to be discovered.");
            });
        }

        /// <summary>
        /// Tests that the shipped checks are components of the core assembly.
        /// </summary>
        /// <param name="type">The health component.</param>
        [Theory]
        [MemberData(nameof(ShippedChecks))]
        public void ShippedCheckBelongsToTheCorePlugin(Type type)
        {
            Assert.Same(typeof(KleeneStarApplication).Assembly, type.Assembly);
            Assert.True(typeof(IHealth).IsAssignableFrom(type));
        }

        /// <summary>
        /// Tests that every shipped check declares a budget below the probe timeout the
        /// Dockerfile and the Kubernetes example allow (five seconds including transport).
        /// </summary>
        /// <param name="type">The health component.</param>
        [Theory]
        [MemberData(nameof(ShippedChecks))]
        public void ShippedCheckFitsTheProbeTimeout(Type type)
        {
            var timeout = type.GetCustomAttribute<HealthTimeoutAttribute>();

            Assert.NotNull(timeout);
            Assert.InRange(timeout.Milliseconds, 1, 4000);
        }
    }
}
