using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebFragment.Class;
using KleeneStar.Model.Entities;
using System.Collections;
using System.Reflection;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.Test.WebFragment
{
    /// <summary>
    /// Pins down the shape of the three class dialogs after the name moved onto the title
    /// bar and the workspace onto the add form: what each dialog carries, and what it
    /// deliberately does not.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestClassDialogShape
    {
        /// <summary>
        /// Reads the items a form fragment registered.
        /// </summary>
        /// <param name="fragment">The fragment.</param>
        /// <returns>The items.</returns>
        private static List<object> Items(object fragment)
        {
            var property = fragment.GetType().GetProperty("Items", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(property);

            return (property!.GetValue(fragment) as IEnumerable)!.Cast<object>().ToList();
        }

        /// <summary>
        /// The edit dialog keeps its name input out of the items - it is rendered onto the
        /// header instead - and marks it as the dialog's title. It stays the availability
        /// check, without the label and the help line a title bar has no room for.
        /// </summary>
        [Fact]
        public void EditDialog_TitlesItselfByTheName()
        {
            CoreHubFixture.Initialize(nameof(EditDialog_TitlesItselfByTheName));

            var fragment = new ClassEditFormFragment(null!);

            Assert.IsType<ControlDataFormItemInputUnique>(fragment.ClassName);
            Assert.DoesNotContain(fragment.ClassName, Items(fragment));
            Assert.Contains(FormTitleInput.Mark, fragment.ClassName.Classes);
            Assert.Null(fragment.ClassName.Label);
            Assert.Null(fragment.ClassName.Help);
            Assert.True(fragment.ClassName.Required(null));
            Assert.Equal(nameof(Model.Entities.Class.Name), fragment.ClassName.Name(null));
        }

        /// <summary>
        /// The add dialog carries the workspace the class is filed in as a hidden input, and
        /// still asks for the name in the body: a new class has no title yet.
        /// </summary>
        [Fact]
        public void AddDialog_CarriesTheWorkspace()
        {
            CoreHubFixture.Initialize(nameof(AddDialog_CarriesTheWorkspace));

            var fragment = new ClassAddFormFragment(null!);
            var items = Items(fragment);

            Assert.IsType<ControlFormItemInputHidden>(fragment.WorkspaceId);
            Assert.Equal(nameof(Model.Entities.Class.WorkspaceId), fragment.WorkspaceId.Name(null));
            Assert.Contains(fragment.WorkspaceId, items);
            Assert.Contains(fragment.ClassName, items);
            Assert.IsType<ControlDataFormItemInputUnique>(fragment.ClassName);
        }

        /// <summary>
        /// The clone dialog asks for the name in the body as well, through the availability
        /// check: a clone is a new class and competes with every name of the workspace.
        /// </summary>
        [Fact]
        public void CloneDialog_ChecksTheName()
        {
            CoreHubFixture.Initialize(nameof(CloneDialog_ChecksTheName));

            var fragment = new ClassCloneFormFragment(null!);

            Assert.IsType<ControlDataFormItemInputUnique>(fragment.ClassName);
            Assert.Contains(fragment.ClassName, Items(fragment));
            Assert.NotNull(fragment.ClassName.ServiceFactory);
        }

        /// <summary>
        /// The workspace binding the class-keyed dialogs need answers nothing without a
        /// request rather than failing, so a dialog rendered outside a request still renders.
        /// </summary>
        [Fact]
        public void WorkspaceKeyOfClass_AnswersNothingWithoutARequest()
        {
            CoreHubFixture.Initialize(nameof(WorkspaceKeyOfClass_AnswersNothingWithoutARequest));

            var bindings = typeof(ClassEditFormFragment).Assembly.GetType("KleeneStar.Core.WebFragment.Class.ClassFormBindings");

            Assert.NotNull(bindings);

            var method = bindings!.GetMethod("WorkspaceKeyOfClass", BindingFlags.Public | BindingFlags.Static);

            Assert.NotNull(method);

            var result = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, string>>>(method!.Invoke(null, [null]));

            Assert.Empty(result);
        }
    }
}
