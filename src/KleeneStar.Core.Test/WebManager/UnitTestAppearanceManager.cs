using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebTheme;
using KleeneStar.Model.Entities;
using System;
using System.Linq;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Tests <see cref="AppearanceManager"/>: the appearance is kept per identity, normalized
    /// on the way in, merged rather than replaced by a partial change, and not kept at all for
    /// nobody or for a record without any choice.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestAppearanceManager
    {
        private static readonly Guid OwnerId = Guid.Parse("A9900000-0000-4000-8000-000000000001");
        private static readonly Guid OtherId = Guid.Parse("A9900000-0000-4000-8000-000000000002");

        /// <summary>
        /// Seeds two identities and acts as the first.
        /// </summary>
        /// <param name="database">The per-test in-memory database name.</param>
        /// <returns>The acting scope.</returns>
        private static IDisposable Seed(string database)
        {
            CoreHubFixture.Initialize(database);

            using var db = CoreHubFixture.CreateDbContext(database);

            db.Identities.AddRange
            (
                new Identity { Id = OwnerId, Name = "Owner", Email = "owner@kleenestar.test", PasswordHash = "$seed$v1$test" },
                new Identity { Id = OtherId, Name = "Other", Email = "other@kleenestar.test", PasswordHash = "$seed$v1$test" }
            );
            db.SaveChanges();

            return CoreHub.SessionManager.BeginIdentity(OwnerId);
        }

        /// <summary>
        /// A saved record reads back normalized and stays the owner's.
        /// </summary>
        [Fact]
        public void SaveThenGetRoundTripsNormalized()
        {
            using var acting = Seed(nameof(SaveThenGetRoundTripsNormalized));

            CoreHub.AppearanceManager.Save(OwnerId, new AppearancePreference
            {
                Theme = " KleeneStar.Theme.WebTheme.SpringTheme ",
                ColorScheme = "DARK",
                AccentColor = "#ABC",
                FontScale = 110,
                Density = "compact",
                ReduceMotion = true
            });

            var read = CoreHub.AppearanceManager.Get(OwnerId);

            Assert.Equal(OwnerId, read.Id);
            Assert.Equal("kleenestar.theme.webtheme.springtheme", read.Theme);
            Assert.Equal("dark", read.ColorScheme);
            Assert.Equal("#aabbcc", read.AccentColor);
            Assert.Equal(110, read.FontScale);
            Assert.Equal(AppearanceOptions.Compact, read.Density);
            Assert.True(read.ReduceMotion);

            Assert.True(CoreHub.AppearanceManager.Get(OtherId).IsEmpty);
        }

        /// <summary>
        /// A change through the acting caller keeps every choice it does not name.
        /// </summary>
        [Fact]
        public void UpdateKeepsTheOtherChoices()
        {
            using var acting = Seed(nameof(UpdateKeepsTheOtherChoices));

            Assert.True(CoreHub.AppearanceManager.Update(null, x => x.ColorScheme = "dark"));
            Assert.True(CoreHub.AppearanceManager.Update(null, x => x.AccentColor = "#112233"));

            var read = CoreHub.AppearanceManager.Get(OwnerId);

            Assert.Equal("dark", read.ColorScheme);
            Assert.Equal("#112233", read.AccentColor);
        }

        /// <summary>
        /// A record without any choice is removed, not kept as an empty row.
        /// </summary>
        [Fact]
        public void EmptyRecordIsNotKept()
        {
            using var acting = Seed(nameof(EmptyRecordIsNotKept));

            CoreHub.AppearanceManager.Save(OwnerId, new AppearancePreference { ColorScheme = "light" });
            Assert.NotNull(CoreHub.SessionManager.GetValue(OwnerId, IAppearanceManager.Scope, IAppearanceManager.Key));

            CoreHub.AppearanceManager.Save(OwnerId, new AppearancePreference { FontScale = 100, Density = "comfortable" });
            Assert.Null(CoreHub.SessionManager.GetValue(OwnerId, IAppearanceManager.Scope, IAppearanceManager.Key));
        }

        /// <summary>
        /// Nobody stores nothing, and a value that no longer reads is no choice.
        /// </summary>
        [Fact]
        public void NobodyAndBrokenValuesReadAsNoChoice()
        {
            using var acting = Seed(nameof(NobodyAndBrokenValuesReadAsNoChoice));

            using (CoreHub.SessionManager.BeginIdentity(Guid.Empty))
            {
                Assert.False(CoreHub.AppearanceManager.Update(null, x => x.ColorScheme = "dark"));
            }

            CoreHub.AppearanceManager.Save(Guid.Empty, new AppearancePreference { ColorScheme = "dark" });
            Assert.True(CoreHub.AppearanceManager.Get(Guid.Empty).IsEmpty);

            CoreHub.SessionManager.SetValue(OwnerId, IAppearanceManager.Scope, IAppearanceManager.Key, "{ not json");
            Assert.True(CoreHub.AppearanceManager.Get(OwnerId).IsEmpty);
        }

        /// <summary>
        /// The form's answers are the stored record seen through the form: the theme's accent
        /// without a custom one, 100 % without a size.
        /// </summary>
        [Fact]
        public void FormAnswersProjectTheRecord()
        {
            var empty = AppearanceForm.From(OwnerId, new AppearancePreference());

            Assert.Equal(AppearanceOptions.AccentTheme, empty.AccentMode);
            Assert.Equal(AppearanceOptions.DefaultAccent, empty.AccentColor);
            Assert.Equal("100", empty.FontScale);
            Assert.Equal(AppearanceOptions.Comfortable, empty.Density);
            Assert.Equal("false", empty.ReduceMotion);

            var custom = AppearanceForm.From(OwnerId, new AppearancePreference { AccentColor = "#123456", FontScale = 90, ReduceMotion = true });

            Assert.Equal(AppearanceOptions.AccentCustom, custom.AccentMode);
            Assert.Equal("#123456", custom.AccentColor);
            Assert.Equal("90", custom.FontScale);
            Assert.Equal("true", custom.ReduceMotion);
            Assert.Contains("90", AppearanceOptions.FontScales.Select(x => x.ToString()));
        }
    }
}
