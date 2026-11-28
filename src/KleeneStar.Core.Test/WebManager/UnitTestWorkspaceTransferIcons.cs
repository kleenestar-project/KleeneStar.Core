using KleeneStar.Core.WebManager;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using KleeneStar.Model.Settings;
using KleeneStar.Model.Transfer;
using Microsoft.EntityFrameworkCore;
using System.Text;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Verifies that workspace structure carries icon bytes between independent installations.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestWorkspaceTransferIcons
    {
        /// <summary>
        /// Exports and imports uploaded and generated icons without retaining source file dependencies.
        /// </summary>
        /// <param name="structureOnly">Whether object rows are excluded from the export.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Transfer_RestoresStructureIcons(bool structureOnly)
        {
            // arrange
            var previous = ModelHub.DatabaseSettings;
            var directory = Path.Combine(Path.GetTempPath(), "kleenestar-transfer-icons-" + Guid.NewGuid().ToString("N"));
            var sourceDirectory = Path.Combine(directory, "source-icons");
            var targetDirectory = Path.Combine(directory, "target-icons");
            Directory.CreateDirectory(sourceDirectory);
            var svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"16\" height=\"16\"><rect width=\"16\" height=\"16\" fill=\"red\"/></svg>");
            var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
            File.WriteAllBytes(Path.Combine(sourceDirectory, "workspace.svg"), svg);
            File.WriteAllBytes(Path.Combine(sourceDirectory, "class.png"), png);
            var sourceIcons = new WorkspaceTransferIcons(sourceDirectory, "/source/assets/icons/");
            var targetIcons = new WorkspaceTransferIcons(targetDirectory, "/target/assets/icons/");
            var workspaceId = Guid.NewGuid();
            var classId = Guid.NewGuid();

            try
            {
                Use(Path.Combine(directory, "source.db"));
                using (var db = ModelHub.CreateDbContext())
                {
                    db.Workspaces.Add(new Workspace(workspaceId) { Key = "ICO", Name = "Icons", Icon = ImageIcon.FromString("/source/assets/icons/workspace.svg") });
                    db.Classes.Add(new Class { Id = classId, WorkspaceId = workspaceId, Name = "Ticket", Icon = ImageIcon.FromString("/source/assets/icons/class.png") });
                    db.Templates.Add(new Template { Id = Guid.NewGuid(), ClassId = classId, Name = "Preset", Icon = ImageIcon.FromString("/source/assets/icons/class.png") });
                    db.SaveChanges();
                }

                // act
                using var archive = new MemoryStream();
                Assert.True(ModelHub.ExportWorkspaces([workspaceId], archive, structureOnly: structureOnly, exportIcon: sourceIcons.Export).Succeeded);
                Directory.Delete(sourceDirectory, recursive: true);
                Use(Path.Combine(directory, "target.db"));
                archive.Position = 0;
                Assert.True(ModelHub.ImportWorkspaces(archive, new WorkspaceImportOptions { DryRun = true }, importIcon: targetIcons.Import).Succeeded);

                // validation
                Assert.False(Directory.Exists(targetDirectory));
                archive.Position = 0;
                Assert.True(ModelHub.ImportWorkspaces(archive, importIcon: targetIcons.Import).Succeeded);
                using var target = ModelHub.CreateDbContext();
                var workspaceIcon = target.Workspaces.Single().Icon.Uri.ToString();
                var classIcon = target.Classes.Single().Icon.Uri.ToString();
                Assert.StartsWith("/target/assets/icons/transfer-", workspaceIcon);
                Assert.StartsWith("/target/assets/icons/transfer-", classIcon);
                Assert.Equal(svg, File.ReadAllBytes(Path.Combine(targetDirectory, Path.GetFileName(workspaceIcon))));
                Assert.Equal(png, File.ReadAllBytes(Path.Combine(targetDirectory, Path.GetFileName(classIcon))));
                Assert.Equal(classIcon, target.Templates.Single().Icon.Uri.ToString());
                Assert.Equal(2, Directory.GetFiles(targetDirectory).Length);
                Assert.True(classIcon.Length < 256);

                archive.Position = 0;
                var skipped = ModelHub.ImportWorkspaces(archive, importIcon: _ => throw new InvalidOperationException("Skipped rows must not restore icons."));
                Assert.False(skipped.Succeeded);
                Assert.Equal("row-present", Assert.Single(skipped.Workspaces).Skipped);

                archive.Position = 0;
                var overwritten = ModelHub.ImportWorkspaces(archive, new WorkspaceImportOptions { Overwrite = true }, importIcon: targetIcons.Import);
                Assert.True(overwritten.Succeeded, overwritten.Error);
                Assert.Equal(2, Directory.GetFiles(targetDirectory).Length);
            }
            finally
            {
                ModelHub.DatabaseSettings = previous;
                Directory.Delete(directory, recursive: true);
            }
        }

        /// <summary>
        /// Keeps unrelated addresses unchanged and prevents local path traversal during export.
        /// </summary>
        /// <param name="address">The address outside the stored icon set.</param>
        [Theory]
        [InlineData("/source/assets/icons/../secret.svg")]
        [InlineData("/source/assets/icons/%2e%2e%2fsecret.svg")]
        [InlineData("https://example.com/icon.png")]
        [InlineData("/kleenestar/assets/icons/form/create.svg")]
        public void Export_PreservesUnrelatedAddresses(string address)
        {
            // arrange
            var icons = new WorkspaceTransferIcons(Path.GetTempPath(), "/source/assets/icons/");
            var icon = ImageIcon.FromString(address);

            // act
            var exported = icons.Export(icon);

            // validation
            Assert.Same(icon, exported);
        }

        /// <summary>
        /// Configures and migrates an isolated SQLite database.
        /// </summary>
        /// <param name="path">The database file path.</param>
        private static void Use(string path)
        {
            ModelHub.DatabaseSettings = new DatabaseSettings
            {
                Provider = "Microsoft.Data.Sqlite",
                Assembly = "KleeneStar.Model.Sqlite",
                ConnectionString = $"Data Source={path};Pooling=False"
            };
            using var db = ModelHub.CreateDbContext();
            db.Database.Migrate();
        }
    }
}
