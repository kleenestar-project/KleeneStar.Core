using KleeneStar.Core.WebNavigator;

namespace KleeneStar.Core.Test.WebNavigator
{
    /// <summary>
    /// Provides unit tests for <see cref="NavigatorLinkAddress"/>.
    /// </summary>
    public class UnitTestNavigatorLinkAddress
    {
        /// <summary>
        /// Verifies that an address which is already unambiguous is passed through untouched.
        /// </summary>
        /// <param name="address">The configured address.</param>
        [Theory]
        [InlineData("https://example.com/a")]
        [InlineData("http://example.com")]
        [InlineData("/kleenestar/workspaces")]
        [InlineData("/")]
        public void Normalize_KeepsUnambiguousAddress(string address)
        {
            Assert.Equal(address, NavigatorLinkAddress.Normalize(address));
        }

        /// <summary>
        /// Verifies that a value which looks like a host becomes an external address, because
        /// without a scheme it would otherwise be read as a relative path and collapse to the root.
        /// </summary>
        /// <param name="address">The configured address.</param>
        /// <param name="expected">The expected normalized address.</param>
        [Theory]
        [InlineData("example.com", "https://example.com")]
        [InlineData("google.de", "https://google.de")]
        [InlineData("example.com/path", "https://example.com/path")]
        public void Normalize_TreatsHostLikeValueAsExternal(string address, string expected)
        {
            Assert.Equal(expected, NavigatorLinkAddress.Normalize(address));
        }

        /// <summary>
        /// Verifies that a value without a dot before the first slash is read as a route of this
        /// server rather than as an external host.
        /// </summary>
        /// <param name="address">The configured address.</param>
        /// <param name="expected">The expected normalized address.</param>
        [Theory]
        [InlineData("workspaces", "/workspaces")]
        [InlineData("settings/tenants", "/settings/tenants")]
        public void Normalize_TreatsPlainValueAsInternalRoute(string address, string expected)
        {
            Assert.Equal(expected, NavigatorLinkAddress.Normalize(address));
        }

        /// <summary>
        /// Verifies that a missing address yields nothing instead of an unusable value.
        /// </summary>
        /// <param name="address">The configured address.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Normalize_ReturnsNullForMissingAddress(string address)
        {
            Assert.Null(NavigatorLinkAddress.Normalize(address));
        }

        /// <summary>
        /// Verifies that surrounding whitespace does not change how an address is classified.
        /// </summary>
        [Fact]
        public void Normalize_TrimsBeforeClassifying()
        {
            Assert.Equal("https://example.com", NavigatorLinkAddress.Normalize("  example.com  "));
        }

        /// <summary>
        /// Verifies which normalized addresses are recognized as pointing at this server.
        /// </summary>
        /// <param name="normalized">The normalized address.</param>
        /// <param name="expected">Whether the address is internal.</param>
        [Theory]
        [InlineData("/workspaces", true)]
        [InlineData("https://example.com", false)]
        [InlineData(null, false)]
        public void IsInternal_ClassifiesNormalizedAddress(string normalized, bool expected)
        {
            Assert.Equal(expected, NavigatorLinkAddress.IsInternal(normalized));
        }
    }
}
