using WebExpress.WebApp.WebData;

namespace KleeneStar.Core.Test.WebFragment
{
    /// <summary>
    /// Guards the data-island detail views against rendering a host element that carries no data
    /// service, which leaves the client control with nothing to load and nowhere to save.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestDataDetailViewFragmentServices
    {
        /// <summary>
        /// Verifies that every concrete data-island detail view declares a data service.
        /// </summary>
        [Fact]
        public void DataDetailViewFragments_DeclareDataServices()
        {
            CoreHubFixture.Initialize(nameof(DataDetailViewFragments_DeclareDataServices));

            var fragments = typeof(CoreHub).Assembly
                .GetTypes()
                .Where(x => !x.IsAbstract)
                .Where(x => typeof(IDataIsland).IsAssignableFrom(x))
                .Where(x => x.Name.EndsWith("DetailViewFragment", StringComparison.Ordinal))
                .OrderBy(x => x.FullName)
                .ToArray();

            Assert.NotEmpty(fragments);

            foreach (var fragmentType in fragments)
            {
                var fragment = Assert.IsAssignableFrom<IDataIsland>
                (
                    Activator.CreateInstance(fragmentType, new object[] { null })
                );

                Assert.True
                (
                    fragment.ServiceFactory is not null,
                    $"{fragmentType.FullName} does not declare a data service."
                );
            }
        }

        /// <summary>
        /// Verifies that the workflow editor is additionally seeded with the state island, because
        /// the control sources the workflow id from it and sends it as the id query parameter of
        /// both the load and the autosave. Without it the endpoint answers with a bad request.
        /// </summary>
        [Fact]
        public void WorkflowDetailViewFragment_DeclaresStateIsland()
        {
            CoreHubFixture.Initialize(nameof(WorkflowDetailViewFragment_DeclaresStateIsland));

            var fragment = new Core.WebFragment.Workflow.WorkflowDetailViewFragment(null);

            Assert.NotNull(fragment.StateFactory);
            Assert.NotNull(fragment.ServiceFactory);
        }
    }
}
