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
        /// Rejects malformed uploads without consuming fields before complete form validation.
        /// </summary>
        [Fact]
        public void InvalidAvatarPreservesPayloadAndReportsField()
        {
            // arrange
            var payload = new RestApiCrudFormData { ["Icon"] = "data:image/png;base64,invalid!" };
            var result = new RestApiValidationResult();

            // act
            AvatarStorage.Validate(payload, "icon", result);

            // validation
            Assert.False(result.IsValid);
            Assert.Equal("icon", Assert.Single(result.Errors).Field);
            Assert.Equal("data:image/png;base64,invalid!", payload["ICON"]);
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
            var resolved = AvatarStorage.Resolve(Guid.NewGuid(), current.Uri.ToString(), current,
                () => throw new InvalidOperationException("An unchanged picture must not invoke storage."));

            // validation
            Assert.True(accepted.IsValid);
            Assert.False(rejected.IsValid);
            Assert.Same(current, resolved);
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
