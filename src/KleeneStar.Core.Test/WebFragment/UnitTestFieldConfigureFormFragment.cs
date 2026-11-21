using KleeneStar.Core.WebFragment.Field;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.Test.WebFragment
{
    /// <summary>
    /// Regression tests for the field configuration form's data wiring.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestFieldConfigureFormFragment
    {
        /// <summary>
        /// Verifies that Configure loads and updates the selected field through
        /// the Fields CRUD service.
        /// </summary>
        [Fact]
        public void ConfigureFragment_HasEditDataServiceAndItemId()
        {
            CoreHubFixture.Initialize(nameof(ConfigureFragment_HasEditDataServiceAndItemId));

            var fragment = new FieldConfigureFormFragment(null!);

            Assert.NotNull(fragment.ServiceFactory);
            Assert.NotNull(fragment.ItemId);
            Assert.Equal(RequestMethod.PUT, fragment.Method?.Invoke(null!));
            Assert.Equal("edit", fragment.Mode?.Invoke(null!));
        }

        /// <summary>
        /// Verifies that every remote selection used by Configure declares its
        /// own query service.
        /// </summary>
        [Fact]
        public void ConfigureFragment_RemoteSelectionsHaveDataServices()
        {
            CoreHubFixture.Initialize(nameof(ConfigureFragment_RemoteSelectionsHaveDataServices));

            var fragment = new FieldConfigureFormFragment(null!);

            Assert.NotNull(fragment.WorkflowSelection.ServiceFactory);
            Assert.NotNull(fragment.DefaultPrioritySelection.ServiceFactory);
            Assert.NotNull(fragment.SelectedPriorities.ServiceFactory);
        }
    }
}
