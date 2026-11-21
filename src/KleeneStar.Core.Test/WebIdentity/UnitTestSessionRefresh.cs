using KleeneStar.Core.WebIdentity;
using KleeneStar.Core.WebManager;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Reflection;
using System.Text.Json;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.Test.WebIdentity
{
    /// <summary>
    /// Tests the session a refresh records and the revocation that ends it, when the refresh
    /// request carries a forged access cookie next to its valid refresh token.
    /// </summary>
    /// <remarks>
    /// On a refresh the framework verifies the renewed token, but the request still carries the
    /// previous access cookie, which nobody verified. The end of the grant decides how long the
    /// session is listed and how long revoking it lasts; read from a forged cookie, a revocation
    /// could expire long before the refresh token and the session would sign in again.
    /// </remarks>
    [Collection("NonParallelTests")]
    public class UnitTestSessionRefresh
    {
        /// <summary>
        /// The name of the access cookie over https, the transport the fixture's host defaults to.
        /// </summary>
        private const string AccessCookie = "__Host-wx-access";

        /// <summary>
        /// A refresh with a valid refresh token and a forged access cookie records the session
        /// with the end of the verified grant, and revoking the session lasts until then.
        /// </summary>
        [Fact]
        public void RefreshWithForgedAccessCookieKeepsTheVerifiedGrant()
        {
            // arrange
            CoreHubFixture.Initialize(nameof(RefreshWithForgedAccessCookieKeepsTheVerifiedGrant));
            var store = new RecordingTokenStore();
            var application = Stub<IApplicationContext>((_, _) => null);
            var tokenStores = Stub<IIdentityTokenStoreManager>((method, _) => method.Name == nameof(IIdentityTokenStoreManager.GetStore) ? store : null);
            var hub = Stub<IComponentHub>((method, _) => method.Name == "get_" + nameof(IComponentHub.IdentityTokenStoreManager) ? tokenStores : null);
            var manager = (IdentitySessionManager)Activator.CreateInstance(typeof(IdentitySessionManager), BindingFlags.NonPublic | BindingFlags.Instance, null, [hub, null], null);
            var applicationProperty = typeof(CoreHub).GetProperty(nameof(CoreHub.ApplicationContext));
            var previousApplication = applicationProperty.GetValue(null);
            applicationProperty.SetValue(null, application);

            try
            {
                var identity = new TestIdentity();
                var grant = "grant-" + Guid.NewGuid().ToString("N");
                var grantExpires = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddHours(8).ToUnixTimeSeconds());
                var forged = Unsigned(new Dictionary<string, object>
                {
                    ["sub"] = identity.Id.ToString(),
                    ["token_use"] = "access",
                    ["jti"] = "forged",
                    ["grant"] = grant,
                    ["grant_exp"] = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds(),
                    ["exp"] = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds()
                });
                var request = CreateRequest($"{AccessCookie}={forged}");

                // the context as the framework builds it on a refresh: the claims of the renewed token
                var context = new IdentityValidationContext
                {
                    Identity = identity,
                    Request = request,
                    ApplicationContext = application,
                    Grant = grant,
                    TokenUse = "access",
                    TokenId = "renewed",
                    IssuedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                    GrantExpiresAt = grantExpires
                };

                // act
                var credential = SessionCredential.From(context);
                var accepted = manager.Accepts(credential, request);
                var session = manager.GetSessions(identity.Id).SingleOrDefault();
                manager.Revoke(session?.Id ?? Guid.Empty);
                var acceptedAfterRevocation = manager.Accepts(credential, request);

                // validation
                Assert.Equal(grantExpires.UtcDateTime, credential.GrantExpires);
                Assert.Equal("renewed", credential.TokenId);
                Assert.True(accepted);
                Assert.NotNull(session);
                Assert.Equal(grantExpires.UtcDateTime, session.Expires);
                Assert.True(store.Revocations.TryGetValue("grant:" + grant, out var revokedUntil));
                Assert.Equal(grantExpires, revokedUntil);
                Assert.False(acceptedAfterRevocation);
            }
            finally
            {
                applicationProperty.SetValue(null, previousApplication);
            }
        }

        /// <summary>
        /// Builds a request that carries the given cookie header.
        /// </summary>
        /// <param name="cookie">The value of the cookie header.</param>
        /// <returns>The request.</returns>
        private static IRequest CreateRequest(string cookie)
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = "POST",
                Protocol = "HTTP/1.1",
                Scheme = "https",
                RawTarget = WebExpress.WebCore.WebIdentity.IdentityManager.RefreshPath,
                QueryString = string.Empty,
                Headers = new HeaderDictionary { ["Host"] = "localhost", ["Cookie"] = cookie }
            });
            features.Set<IHttpConnectionFeature>(new HttpConnectionFeature
            {
                LocalIpAddress = System.Net.IPAddress.Loopback,
                RemoteIpAddress = System.Net.IPAddress.Loopback
            });
            features.Set<IHttpRequestIdentifierFeature>(new HttpRequestIdentifierFeature
            {
                TraceIdentifier = nameof(UnitTestSessionRefresh)
            });

            return new WebExpress.WebCore.WebMessage.HttpContext(features, null!).Request;
        }

        /// <summary>
        /// Builds a token with the given claims and no signature, as an attacker can put into a
        /// cookie without knowing the signing key.
        /// </summary>
        /// <param name="claims">The claims of the payload.</param>
        /// <returns>The serialized token.</returns>
        private static string Unsigned(IDictionary<string, object> claims)
        {
            static string Encode(object value) => Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(JsonSerializer.Serialize(value));

            return $"{Encode(new Dictionary<string, string> { ["alg"] = "none", ["typ"] = "JWT" })}.{Encode(claims)}.";
        }

        /// <summary>
        /// Creates a stand-in for an interface whose members the handler answers.
        /// </summary>
        /// <typeparam name="T">The interface.</typeparam>
        /// <param name="handler">Answers a call; null stands for the default of the return type.</param>
        /// <returns>The stand-in.</returns>
        private static T Stub<T>(Func<MethodInfo, object[], object> handler) where T : class
        {
            var proxy = DispatchProxy.Create<T, StubProxy>();
            ((StubProxy)(object)proxy).Handler = handler;

            return proxy;
        }

        /// <summary>
        /// Forwards the calls of a stand-in to its handler.
        /// </summary>
        public class StubProxy : DispatchProxy
        {
            /// <summary>
            /// Gets or sets the handler that answers the calls.
            /// </summary>
            public Func<MethodInfo, object[], object> Handler { get; set; }

            /// <summary>
            /// Answers a call through the handler.
            /// </summary>
            /// <param name="targetMethod">The member called.</param>
            /// <param name="args">The arguments.</param>
            /// <returns>The answer, or the default of the return type.</returns>
            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                var type = targetMethod.ReturnType;

                return Handler?.Invoke(targetMethod, args)
                    ?? (type.IsValueType && type != typeof(void) ? Activator.CreateInstance(type) : null);
            }
        }

        /// <summary>
        /// A token store that records how long each revocation lasts.
        /// </summary>
        private sealed class RecordingTokenStore : IIdentityTokenStore
        {
            /// <summary>
            /// Gets the revocations and the time each one lasts until.
            /// </summary>
            public Dictionary<string, DateTimeOffset> Revocations { get; } = [];

            /// <summary>
            /// Records a marker unless it exists.
            /// </summary>
            /// <param name="tokenId">The marker.</param>
            /// <param name="expiresAt">The retention deadline.</param>
            /// <returns>True when this call created the marker.</returns>
            public bool TryConsume(string tokenId, DateTimeOffset expiresAt)
            {
                return Revocations.TryAdd(tokenId, expiresAt);
            }

            /// <summary>
            /// Records a revocation and how long it lasts.
            /// </summary>
            /// <param name="tokenId">The marker.</param>
            /// <param name="expiresAt">The retention deadline.</param>
            public void Revoke(string tokenId, DateTimeOffset expiresAt)
            {
                Revocations[tokenId] = expiresAt;
            }

            /// <summary>
            /// Reports whether a marker exists.
            /// </summary>
            /// <param name="tokenId">The marker.</param>
            /// <returns>True when it exists.</returns>
            public bool IsRevoked(string tokenId)
            {
                return Revocations.ContainsKey(tokenId);
            }
        }

        /// <summary>
        /// An identity the test signs in with.
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
