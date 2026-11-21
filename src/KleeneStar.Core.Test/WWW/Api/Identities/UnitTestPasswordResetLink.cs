using System.Reflection;
using PasswordResets = KleeneStar.Core.WWW.Api._1_.Identities.PasswordResets;

namespace KleeneStar.Core.Test.WWW.Api.Identities
{
    /// <summary>
    /// Provides unit tests for the address the one-time password reset link is built on.
    /// </summary>
    /// <remarks>
    /// The link is copied out of the dialog and opened elsewhere, so it has to name an address
    /// that is reachable from outside. The sitemap builds on <c>WebExpress:ExternalUri</c> when
    /// it is set and on the listener binding otherwise - <c>0.0.0.0</c> in the container.
    /// </remarks>
    public class UnitTestPasswordResetLink
    {
        private const string Page = "/kleenestar/setpassword";

        /// <summary>
        /// Tests that a page the sitemap built on the configured public address is kept, whatever
        /// the request says.
        /// </summary>
        [Fact]
        public void ConfiguredPublicAddressWins()
        {
            var link = Anchor("https://kleenestar.example.org" + Page, "https://kleenestar.example.org/", "http://ks:8080/kleenestar/api/1/identities/passwordresets", "ks:8081");

            Assert.Equal("https://kleenestar.example.org" + Page, link);
        }

        /// <summary>
        /// Tests that without a public address the listener binding is replaced by the origin
        /// the administrator's browser used, mapped port included.
        /// </summary>
        [Fact]
        public void ListenerBindingIsReplacedByTheBrowserOrigin()
        {
            var link = Anchor("http://0.0.0.0:8080" + Page, null, "http://ks:8080/kleenestar/api/1/identities/passwordresets", "ks:8081");

            Assert.Equal("http://ks:8081" + Page, link);
        }

        /// <summary>
        /// Tests that a relative page address is placed at the browser origin.
        /// </summary>
        [Fact]
        public void RelativePageIsPlacedAtTheBrowserOrigin()
        {
            var link = Anchor(Page, "", "http://server/kleenestar/api/1/identities/passwordresets", "server");

            Assert.Equal("http://server" + Page, link);
        }

        /// <summary>
        /// Tests that a missing or malformed <c>Host</c> header falls back to the request's own
        /// authority rather than putting arbitrary text in front of the secret.
        /// </summary>
        /// <param name="host">The header value.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("evil.example/phish")]
        [InlineData("user@evil.example")]
        [InlineData("evil.example?x=")]
        public void UnusableHostHeaderFallsBackToTheRequest(string host)
        {
            var link = Anchor("http://0.0.0.0:8080" + Page, null, "http://ks:8080/kleenestar/api/1/identities/passwordresets", host);

            Assert.Equal("http://ks:8080" + Page, link);
        }

        /// <summary>
        /// Tests that the page address is kept when no request is there to anchor it.
        /// </summary>
        [Fact]
        public void NoRequestKeepsThePage()
        {
            Assert.Equal("http://0.0.0.0:8080" + Page, Anchor("http://0.0.0.0:8080" + Page, null, null, null));
        }

        /// <summary>
        /// Invokes the private anchoring rule of the endpoint.
        /// </summary>
        private static string Anchor(string page, string externalUri, string requestUri, string requestHost)
        {
            var method = typeof(PasswordResets).GetMethod("Anchor", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("PasswordResets.Anchor not found.");

            return (string)method.Invoke(null, [page, externalUri, requestUri, requestHost]);
        }
    }
}
