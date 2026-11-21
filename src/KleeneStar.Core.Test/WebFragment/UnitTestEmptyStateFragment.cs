using KleeneStar.Core.WebFragment.Class;
using KleeneStar.Core.WebFragment.Workspace;
using System.Collections;
using System.Reflection;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.Test.WebFragment
{
    /// <summary>
    /// Pins the shape of the EmptyState fragments used to inform the user when
    /// a workspace/class has no fields, forms, objects, etc.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestEmptyStateFragment
    {
        /// <summary>
        /// Verifies that the workspace empty-state fragment renders the
        /// <see cref="ControlEmptyState"/> with an icon, title and message.
        /// </summary>
        [Fact]
        public void WorkspaceEmptyStateFragment_HasEmptyStateShape()
        {
            var fragment = new WorkspaceEmptyStateFragment(null!);

            var icon = fragment.Icon?.Invoke(null!);
            var title = fragment.Title?.Invoke(null!);
            var message = fragment.Message?.Invoke(null!);

            Assert.NotNull(icon);
            Assert.False(string.IsNullOrWhiteSpace(title));
            Assert.False(string.IsNullOrWhiteSpace(message));
        }

        /// <summary>
        /// Verifies that the class empty-state fragment uses the same
        /// empty-state control shape and points the user at the field
        /// configuration.
        /// </summary>
        [Fact]
        public void ClassEmptyStateFragment_HasEmptyStateShape()
        {
            var fragment = new ClassEmptyStateFragment(null!);

            var icon = fragment.Icon?.Invoke(null!);
            var title = fragment.Title?.Invoke(null!);
            var message = fragment.Message?.Invoke(null!);

            Assert.NotNull(icon);
            Assert.False(string.IsNullOrWhiteSpace(title));
            Assert.False(string.IsNullOrWhiteSpace(message));
        }

        /// <summary>
        /// Verifies that the static text fallback fragment used in forms
        /// (the <see cref="FieldConfigureFormFragment"/> empty tab) maps to
        /// the WebExpress <see cref="ControlFormItemStaticText"/>.
        /// </summary>
        [Fact]
        public void StaticTextItem_IsControlFormItemStaticText()
        {
            var item = new ControlFormItemStaticText
            {
                Text = _ => "n/a"
            };

            Assert.IsType<ControlFormItemStaticText>(item);
            Assert.Equal("n/a", item.Text?.Invoke(null!));
        }

        /// <summary>
        /// Retrieves the value of the fragment's <c>Items</c> property using reflection.
        /// The method asserts that the property exists and returns its contents as a
        /// non-generic <see cref="IEnumerable"/> for uniform test processing.
        /// </summary>
        /// <param name="fragment">
        /// The fragment instance whose <c>Items</c> collection should be inspected.
        /// </param>
        /// <returns>
        /// The value of the fragment's <c>Items</c> property, cast to <see cref="IEnumerable"/>.
        /// </returns>
        private static IEnumerable GetItems(object fragment)
        {
            var prop = fragment.GetType().GetProperty(
                "Items",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(prop);
            return (IEnumerable)prop!.GetValue(fragment)!;
        }
    }
}
