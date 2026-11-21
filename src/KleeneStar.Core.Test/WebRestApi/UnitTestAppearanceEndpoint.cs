using KleeneStar.Core.WebRestApi;
using KleeneStar.Core.WebTheme;
using KleeneStar.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.Test.WebRestApi
{
    /// <summary>
    /// Tests the write side of the appearance: the endpoint answers <c>PUT</c> and <c>PATCH</c>,
    /// so a payload naming one field changes that field only, and two changes of the same person
    /// made at the same time both survive.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestAppearanceEndpoint
    {
        private static readonly Guid OwnerId = Guid.Parse("A9900000-0000-4000-8000-000000000011");

        /// <summary>
        /// Seeds the caller with a record carrying every adjustment, and acts as the caller.
        /// </summary>
        /// <param name="database">The per-test in-memory database name.</param>
        /// <returns>The acting scope.</returns>
        private static IDisposable Seed(string database)
        {
            CoreHubFixture.Initialize(database);

            using (var db = CoreHubFixture.CreateDbContext(database))
            {
                db.Identities.Add(new Identity { Id = OwnerId, Name = "Owner", Email = "owner@kleenestar.test", PasswordHash = "$seed$v1$test" });
                db.SaveChanges();
            }

            CoreHub.AppearanceManager.Save(OwnerId, new AppearancePreference
            {
                Theme = "kleenestar.theme.webtheme.wintertheme",
                ColorScheme = "dark",
                AccentColor = "#112233",
                FontScale = 125,
                Density = AppearanceOptions.Compact,
                ReduceMotion = true
            });

            return CoreHub.SessionManager.BeginIdentity(OwnerId);
        }

        /// <summary>
        /// Calls the endpoint's update with a payload, as the acting caller (no request: a
        /// passed request would read as anonymous in a test).
        /// </summary>
        /// <param name="entries">The payload.</param>
        private static void Update(params (string Key, object Value)[] entries)
        {
            var endpoint = new global::KleeneStar.Core.WWW.Api._1_.Appearance.Index();
            var update = typeof(RestApiAppearance).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(AppearanceForm), typeof(RestApiCrudFormData), typeof(IRequest)]);

            Assert.NotNull(update);

            update!.Invoke(endpoint, [AppearanceForm.From(OwnerId, CoreHub.AppearanceManager.Get(OwnerId)), Payload(entries), null]);
        }

        /// <summary>
        /// Calls the endpoint's validation with a payload.
        /// </summary>
        /// <param name="entries">The payload.</param>
        /// <returns>The validation result.</returns>
        private static IRestApiValidationResult Validate(params (string Key, object Value)[] entries)
        {
            var endpoint = new global::KleeneStar.Core.WWW.Api._1_.Appearance.Index();
            var validate = typeof(RestApiAppearance).GetMethod("Validate", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(validate);

            return Assert.IsAssignableFrom<IRestApiValidationResult>(validate!.Invoke(endpoint, [AppearanceForm.From(OwnerId, new AppearancePreference()), Payload(entries), CreateRequest()]));
        }

        /// <summary>
        /// Builds a payload the way the framework does, keys lower-cased.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <returns>The payload.</returns>
        private static RestApiCrudFormData Payload((string Key, object Value)[] entries)
        {
            var payload = new RestApiCrudFormData();

            foreach (var (key, value) in entries)
            {
                payload[key.ToLowerInvariant()] = value;
            }

            return payload;
        }

        /// <summary>
        /// A PATCH naming only the font size changes the font size and nothing else.
        /// </summary>
        [Fact]
        public void PartialPayloadChangesOnlyWhatItNames()
        {
            using var acting = Seed(nameof(PartialPayloadChangesOnlyWhatItNames));

            Update((RestApiAppearance.FontScaleField, "110"));

            var read = CoreHub.AppearanceManager.Get(OwnerId);

            Assert.Equal(110, read.FontScale);
            Assert.Equal("#112233", read.AccentColor);
            Assert.Equal(AppearanceOptions.Compact, read.Density);
            Assert.True(read.ReduceMotion);
            Assert.Equal("dark", read.ColorScheme);
            Assert.Equal("kleenestar.theme.webtheme.wintertheme", read.Theme);
        }

        /// <summary>
        /// The accent answers: the theme's clears it, a custom one without a color keeps the
        /// kept color, a color alone sets it.
        /// </summary>
        [Fact]
        public void AccentAnswersMergeWithTheStoredColor()
        {
            using var acting = Seed(nameof(AccentAnswersMergeWithTheStoredColor));

            Update((RestApiAppearance.AccentModeField, AppearanceOptions.AccentCustom));
            Assert.Equal("#112233", CoreHub.AppearanceManager.Get(OwnerId).AccentColor);

            Update((RestApiAppearance.AccentColorField, "#ABCDEF"));
            Assert.Equal("#abcdef", CoreHub.AppearanceManager.Get(OwnerId).AccentColor);

            Update((RestApiAppearance.AccentModeField, AppearanceOptions.AccentTheme), (RestApiAppearance.AccentColorField, "#ff0000"));
            var read = CoreHub.AppearanceManager.Get(OwnerId);

            Assert.Null(read.AccentColor);
            Assert.Equal(125, read.FontScale);
        }

        /// <summary>
        /// The full form still sets every adjustment, including back to the defaults.
        /// </summary>
        [Fact]
        public void FullPayloadSetsEverything()
        {
            using var acting = Seed(nameof(FullPayloadSetsEverything));

            Update
            (
                (RestApiAppearance.AccentModeField, AppearanceOptions.AccentTheme),
                (RestApiAppearance.AccentColorField, AppearanceOptions.DefaultAccent),
                (RestApiAppearance.FontScaleField, "100"),
                (RestApiAppearance.DensityField, AppearanceOptions.Comfortable),
                (RestApiAppearance.ReduceMotionField, "false")
            );

            var read = CoreHub.AppearanceManager.Get(OwnerId);

            Assert.False(read.HasAdjustments);
            Assert.Equal("dark", read.ColorScheme);
        }

        /// <summary>
        /// A value that is sent but not offered is refused instead of resetting the choice;
        /// an empty payload is no error.
        /// </summary>
        [Fact]
        public void UnofferedValuesAreRefused()
        {
            using var acting = Seed(nameof(UnofferedValuesAreRefused));

            Assert.True(Validate().IsValid);
            Assert.True(Validate((RestApiAppearance.FontScaleField, "90")).IsValid);

            Assert.Contains(Validate((RestApiAppearance.FontScaleField, "115")).Errors, x => x.Field == RestApiAppearance.FontScaleField);
            Assert.Contains(Validate((RestApiAppearance.DensityField, "tight")).Errors, x => x.Field == RestApiAppearance.DensityField);
            Assert.Contains(Validate((RestApiAppearance.ReduceMotionField, "maybe")).Errors, x => x.Field == RestApiAppearance.ReduceMotionField);
            Assert.Contains(Validate((RestApiAppearance.AccentModeField, "neon")).Errors, x => x.Field == RestApiAppearance.AccentModeField);
            Assert.Contains(Validate((RestApiAppearance.AccentColorField, "red")).Errors, x => x.Field == RestApiAppearance.AccentColorField);
        }

        /// <summary>
        /// Two changes of the same person started at the same moment - the theme selector and
        /// the dark mode switch - both survive, round after round.
        /// </summary>
        [Fact]
        public void ConcurrentChangesBothSurvive()
        {
            using var acting = Seed(nameof(ConcurrentChangesBothSurvive));

            for (var round = 0; round < 25; round++)
            {
                CoreHub.AppearanceManager.Save(OwnerId, new AppearancePreference());

                using var start = new Barrier(2);
                var theme = Task.Run(() => { start.SignalAndWait(); CoreHub.AppearanceManager.Update(null, x => x.Theme = "kleenestar.theme.webtheme.springtheme"); });
                var scheme = Task.Run(() => { start.SignalAndWait(); CoreHub.AppearanceManager.Update(null, x => x.ColorScheme = "dark"); });

                Task.WaitAll(theme, scheme);

                var read = CoreHub.AppearanceManager.Get(OwnerId);

                Assert.Equal("kleenestar.theme.webtheme.springtheme", read.Theme);
                Assert.Equal("dark", read.ColorScheme);
            }
        }

        /// <summary>
        /// Builds a request carrying a culture, which validation's translations read.
        /// </summary>
        /// <returns>The request.</returns>
        private static IRequest CreateRequest()
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = "PATCH",
                Protocol = "HTTP/1.1",
                Scheme = "http",
                RawTarget = "/kleenestar/api/1/appearance",
                QueryString = string.Empty,
                Headers = new HeaderDictionary { ["Host"] = "localhost", ["Accept-Language"] = "en" }
            });
            features.Set<IHttpConnectionFeature>(new HttpConnectionFeature
            {
                LocalIpAddress = System.Net.IPAddress.Loopback,
                RemoteIpAddress = System.Net.IPAddress.Loopback
            });
            features.Set<IHttpRequestIdentifierFeature>(new HttpRequestIdentifierFeature
            {
                TraceIdentifier = nameof(UnitTestAppearanceEndpoint)
            });

            return new WebExpress.WebCore.WebMessage.HttpContext(features, null!).Request;
        }
    }
}
