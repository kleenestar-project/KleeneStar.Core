using WebExpress.WebApp.WebControl;

namespace KleeneStar.Core.Test.WebFragment
{
    /// <summary>
    /// Guards the data-service wiring required for clone forms to load the
    /// source record before a new copy is submitted.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestCloneFormFragments
    {
        /// <summary>
        /// Verifies that every clone fragment declares a form data service and
        /// resolves the identifier of the record being cloned.
        /// </summary>
        [Fact]
        public void CloneFragments_HaveDataServiceAndItemId()
        {
            CoreHubFixture.Initialize(nameof(CloneFragments_HaveDataServiceAndItemId));

            var cloneTypes = typeof(CoreHub).Assembly
                .GetTypes()
                .Where(x => !x.IsAbstract)
                .Where(x => typeof(ControlDataFormClone).IsAssignableFrom(x))
                .Where(x => x.Name.EndsWith("CloneFormFragment", StringComparison.Ordinal))
                .OrderBy(x => x.FullName)
                .ToArray();

            Assert.NotEmpty(cloneTypes);

            foreach (var cloneType in cloneTypes)
            {
                var fragment = Assert.IsAssignableFrom<ControlDataFormClone>
                (
                    Activator.CreateInstance(cloneType, new object[] { null })
                );

                Assert.True
                (
                    fragment.ServiceFactory is not null,
                    $"{cloneType.FullName} does not declare a data service."
                );
                Assert.True
                (
                    fragment.ItemId is not null,
                    $"{cloneType.FullName} does not resolve the source item id."
                );
            }
        }
    }
}
