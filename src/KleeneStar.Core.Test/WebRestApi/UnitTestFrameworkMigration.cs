using KleeneStar.Core.WebRestApi;
using System.Text.Json;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.Test.WebRestApi
{
    /// <summary>
    /// Protects application compatibility when framework contracts replace local implementations.
    /// </summary>
    public class UnitTestFrameworkMigration
    {
        /// <summary>
        /// Leaves an inline picture to the framework's image hooks and keeps the formats the
        /// icon route accepts beyond the framework defaults.
        /// </summary>
        [Fact]
        public void InlineAvatarIsLeftToTheFrameworkHooks()
        {
            // arrange
            var payload = new RestApiCrudFormData { ["Icon"] = "data:image/png;base64,invalid!" };
            var result = new RestApiValidationResult();

            // act
            AvatarStorage.Validate(payload, "icon", result);

            // validation
            Assert.True(result.IsValid);
            Assert.Equal("data:image/png;base64,invalid!", payload["ICON"]);
            Assert.True(AvatarStorage.Options.Formats.ContainsKey("image/svg+xml"));
            Assert.True(AvatarStorage.Options.Formats.ContainsKey("image/jpg"));
        }

        /// <summary>
        /// Accepts an unchanged loaded avatar while refusing arbitrary replacement URLs.
        /// </summary>
        [Fact]
        public void LoadedAvatarIsAnUnchangedValue()
        {
            // arrange
            var current = ImageIcon.FromString("https://example.test/icons/current.png");
            var unchanged = new RestApiCrudFormData { ["Icon"] = current.Uri.ToString() };
            var replaced = new RestApiCrudFormData { ["Icon"] = "https://example.test/icons/other.png" };

            // act
            var accepted = AvatarStorage.Validate(unchanged, "Icon", new RestApiValidationResult(), current);
            var rejected = AvatarStorage.Validate(replaced, "Icon", new RestApiValidationResult(), current);

            // validation
            Assert.True(accepted.IsValid);
            Assert.False(rejected.IsValid);
        }

        /// <summary>
        /// Keeps the address of a new record, and gives one without a usable picture the fallback.
        /// </summary>
        [Fact]
        public void AdoptKeepsAddressesAndFallsBack()
        {
            // arrange
            var address = ImageIcon.FromString("https://example.test/icons/current.png");
            var fallback = ImageIcon.FromString("https://example.test/icons/fallback.png");
            var broken = ImageIcon.FromString("data:image/png;base64,invalid!");

            // act
            var kept = AvatarStorage.Adopt(Guid.NewGuid(), address, () => fallback);
            var missing = AvatarStorage.Adopt(Guid.NewGuid(), null, () => fallback);
            var undecodable = AvatarStorage.Adopt(Guid.NewGuid(), broken, () => fallback);

            // validation
            Assert.Same(address, kept);
            Assert.Same(fallback, missing);
            Assert.Same(fallback, undecodable);
        }

        /// <summary>
        /// Writes legacy saved-search layouts in the framework format without losing their settings.
        /// </summary>
        [Fact]
        public void LegacyLayoutUpgradesToFrameworkSnapshot()
        {
            // arrange
            const string legacy = """[{"id":"summary","visible":false,"width":240}]""";
            var columns = new[]
            {
                new RestApiTableColumn { Id = "key", Visible = true },
                new RestApiTableColumn { Id = "summary", Visible = true }
            };

            // act
            var upgraded = TableLayout.Serialize(TableLayout.Parse(legacy));
            var applied = RestApiTableLayout.Parse(upgraded).Apply(columns);

            // validation
            using var json = JsonDocument.Parse(upgraded);
            Assert.Equal(1, json.RootElement.GetProperty("version").GetInt32());
            Assert.Equal("summary", applied[0].Id);
            Assert.False(applied[0].Visible);
            Assert.Equal(240u, applied[0].Width);
            Assert.Equal("key", applied[1].Id);
            Assert.True(columns[1].Visible);
        }

        /// <summary>
        /// Keeps an invalid optional reference distinct from an explicit removal.
        /// </summary>
        [Fact]
        public void InvalidReferenceDoesNotClearExistingReference()
        {
            // arrange
            var payload = new RestApiCrudFormData { ["ParentId"] = "invalid" };

            // act
            var submitted = payload.TryGetGuidReference("ParentId", out var value);

            // validation
            Assert.False(submitted);
            Assert.Null(value);
        }
    }
}
