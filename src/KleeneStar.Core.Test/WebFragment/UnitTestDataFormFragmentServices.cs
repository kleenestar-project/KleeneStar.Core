using WebExpress.WebApp.WebControl;

namespace KleeneStar.Core.Test.WebFragment
{
    /// <summary>
    /// Guards all data-form fragments against using the form redirect URI as a
    /// REST service endpoint.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestDataFormFragmentServices
    {
        /// <summary>
        /// Verifies that every concrete data-form fragment declares a service and
        /// that no fragment assigns an API endpoint to ControlForm.Uri.
        /// </summary>
        [Fact]
        public void DataFormFragments_UseDataServicesInsteadOfApiUris()
        {
            CoreHubFixture.Initialize(nameof(DataFormFragments_UseDataServicesInsteadOfApiUris));

            var fragments = typeof(CoreHub).Assembly
                .GetTypes()
                .Where(x => !x.IsAbstract)
                .Where(x => typeof(ControlDataForm).IsAssignableFrom(x))
                .Where(x => x.Name.EndsWith("FormFragment", StringComparison.Ordinal))
                .OrderBy(x => x.FullName)
                .ToArray();

            Assert.NotEmpty(fragments);

            foreach (var fragmentType in fragments)
            {
                var fragment = Assert.IsType<ControlDataForm>
                (
                    Activator.CreateInstance(fragmentType, [null]),
                    exactMatch: false
                );

                Assert.True
                (
                    fragment.ServiceFactory is not null,
                    $"{fragmentType.FullName} does not declare a data service."
                );
            }

            var sourceRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "KleeneStar.Core", "WebFragment"));

            // only the files of the fragments collected above are scanned. The pattern
            // stands for a redirect target that was mistaken for a service endpoint, which
            // is a property of ControlForm; a file merely named "…FormFragment.cs" may hold
            // something else entirely — a wizard, whose pages carry a Uri of their own to be
            // loaded from — and must not be read as an offender.
            var files = fragments
                .Select(x => x.Name + ".cs")
                .ToHashSet(StringComparer.Ordinal);

            var offenders = Directory
                .EnumerateFiles(sourceRoot, "*FormFragment.cs", SearchOption.AllDirectories)
                .Where(path => files.Contains(Path.GetFileName(path)))
                .Where(path => File.ReadAllText(path).Contains(
                    "Uri = _ => CoreHub.GetUri<global::KleeneStar.Core.WWW.Api",
                    StringComparison.Ordinal))
                .Select(path => Path.GetRelativePath(sourceRoot, path))
                .OrderBy(path => path)
                .ToArray();

            Assert.True(
                offenders.Length == 0,
                $"API endpoint assigned to ControlForm.Uri: {string.Join(", ", offenders)}");
        }
    }
}
