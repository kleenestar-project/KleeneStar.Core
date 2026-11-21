using KleeneStar.Core.WebNavigator;
using System;
using System.Net;

namespace KleeneStar.Core.Test.WebNavigator
{
    /// <summary>
    /// Provides unit tests for <see cref="FaviconResolver"/>. The tests cover the parts that decide
    /// what is requested and what is extracted, so they need no network of their own.
    /// </summary>
    public class UnitTestFaviconResolver
    {
        /// <summary>
        /// Verifies that a declared icon is found and resolved against the address the document was
        /// served from.
        /// </summary>
        [Fact]
        public void ParseIconHref_ResolvesRelativeDeclaration()
        {
            var html = "<html><head><link rel=\"icon\" href=\"/assets/fav.png\"></head></html>";

            Assert.Equal
            (
                "https://example.com/assets/fav.png",
                FaviconResolver.ParseIconHref(html, new Uri("https://example.com/start"))
            );
        }

        /// <summary>
        /// Verifies that an attribute is read regardless of how it is quoted, since the pattern
        /// offers one group per quoting style.
        /// </summary>
        /// <param name="tag">The link element.</param>
        [Theory]
        [InlineData("<link rel=\"icon\" href=\"/a.png\">")]
        [InlineData("<link rel='icon' href='/a.png'>")]
        [InlineData("<link rel=icon href=/a.png>")]
        public void ParseIconHref_ReadsEveryQuotingStyle(string tag)
        {
            Assert.Equal
            (
                "https://example.com/a.png",
                FaviconResolver.ParseIconHref(tag, new Uri("https://example.com/"))
            );
        }

        /// <summary>
        /// Verifies that a sized declaration is preferred over an unsized one, so the choice stays
        /// stable on a site that declares several icons.
        /// </summary>
        [Fact]
        public void ParseIconHref_PrefersSizedDeclaration()
        {
            var html = "<link rel=\"icon\" href=\"/small.png\">"
                     + "<link rel=\"icon\" sizes=\"64x64\" href=\"/large.png\">";

            Assert.Equal
            (
                "https://example.com/large.png",
                FaviconResolver.ParseIconHref(html, new Uri("https://example.com/"))
            );
        }

        /// <summary>
        /// Verifies that a plain icon wins over a touch icon.
        /// </summary>
        [Fact]
        public void ParseIconHref_PrefersIconOverTouchIcon()
        {
            var html = "<link rel=\"apple-touch-icon\" href=\"/touch.png\">"
                     + "<link rel=\"icon\" href=\"/icon.png\">";

            Assert.Equal
            (
                "https://example.com/icon.png",
                FaviconResolver.ParseIconHref(html, new Uri("https://example.com/"))
            );
        }

        /// <summary>
        /// Verifies that declarations which cannot become a usable address are skipped.
        /// </summary>
        /// <param name="html">The document text.</param>
        [Theory]
        [InlineData("<link rel=\"stylesheet\" href=\"/a.css\">")]
        [InlineData("<link rel=\"icon\" href=\"data:image/png;base64,AAAA\">")]
        [InlineData("<link rel=\"icon\">")]
        [InlineData("")]
        [InlineData(null)]
        public void ParseIconHref_SkipsUnusableDeclaration(string html)
        {
            Assert.Null(FaviconResolver.ParseIconHref(html, new Uri("https://example.com/")));
        }

        /// <summary>
        /// Verifies that an entity encoded address is decoded, so a query separator survives.
        /// </summary>
        [Fact]
        public void ParseIconHref_DecodesEntities()
        {
            var html = "<link rel=\"icon\" href=\"/i.png?a=1&amp;b=2\">";

            Assert.Equal
            (
                "https://example.com/i.png?a=1&b=2",
                FaviconResolver.ParseIconHref(html, new Uri("https://example.com/"))
            );
        }

        /// <summary>
        /// Verifies that the addresses which would let a target reach back into this host or its
        /// network are rejected.
        /// </summary>
        /// <param name="address">The address to classify.</param>
        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("127.13.4.9")]
        [InlineData("10.0.0.5")]
        [InlineData("172.16.3.9")]
        [InlineData("172.31.255.254")]
        [InlineData("192.168.1.10")]
        [InlineData("169.254.169.254")]
        [InlineData("100.64.0.1")]
        [InlineData("0.0.0.0")]
        [InlineData("224.0.0.1")]
        [InlineData("::1")]
        [InlineData("fe80::1")]
        [InlineData("fc00::1")]
        [InlineData("::ffff:127.0.0.1")]
        [InlineData("::ffff:10.0.0.1")]
        public void IsPubliclyRoutable_RejectsInternalAddress(string address)
        {
            Assert.False(FaviconResolver.IsPubliclyRoutable(IPAddress.Parse(address)));
        }

        /// <summary>
        /// Verifies that ordinary public addresses remain allowed, so the guard does not block the
        /// case it exists to serve.
        /// </summary>
        /// <param name="address">The address to classify.</param>
        [Theory]
        [InlineData("93.184.216.34")]
        [InlineData("8.8.8.8")]
        [InlineData("172.32.0.1")]
        [InlineData("172.15.255.255")]
        [InlineData("2606:2800:220:1:248:1893:25c8:1946")]
        public void IsPubliclyRoutable_AllowsPublicAddress(string address)
        {
            Assert.True(FaviconResolver.IsPubliclyRoutable(IPAddress.Parse(address)));
        }

        /// <summary>
        /// Verifies that a missing address is rejected rather than treated as routable.
        /// </summary>
        [Fact]
        public void IsPubliclyRoutable_RejectsNull()
        {
            Assert.False(FaviconResolver.IsPubliclyRoutable(null));
        }

        /// <summary>
        /// Verifies that an address which needs no outbound request resolves to nothing, so the
        /// caller falls back without the resolver touching the network.
        /// </summary>
        /// <param name="address">The configured address.</param>
        [Theory]
        [InlineData("/kleenestar/workspaces")]
        [InlineData("workspaces")]
        [InlineData("ftp://example.com")]
        [InlineData("javascript:alert(1)")]
        [InlineData(null)]
        [InlineData("")]
        public async Task ResolveAsync_ReturnsNullWithoutRequest(string address)
        {
            Assert.Null(await FaviconResolver.ResolveAsync(address, TestContext.Current.CancellationToken));
        }
    }
}
