using KleeneStar.Core.WebControl;
using KleeneStar.Model.Entities;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.Test.WebControl
{
    /// <summary>
    /// Provides unit tests for <see cref="ObjectFieldSheet"/> - the *Details* sheet of the object
    /// page and the preview pane: which input a field type is edited with in place, and how a
    /// stored value is seeded into it.
    /// </summary>
    public class UnitTestObjectFieldSheet
    {
        /// <summary>
        /// Creates a field of the given type.
        /// </summary>
        private static Field Field(FieldType type, params string[] options) => new()
        {
            Name = "Probe",
            FieldType = type,
            Options = [.. options]
        };

        /// <summary>
        /// Verifies that one-of-a-few values are edited with the selection, whose read view is a
        /// chip, rather than with the wide controls of the edit mask.
        /// </summary>
        [Theory]
        [InlineData(FieldType.Boolean)]
        [InlineData(FieldType.Selection)]
        [InlineData(FieldType.Choice)]
        [InlineData(FieldType.Radio)]
        [InlineData(FieldType.Tile)]
        [InlineData(FieldType.MultiSelection)]
        public void CreateInlineInput_Options_IsSelection(FieldType type)
        {
            var input = ObjectFieldSheet.CreateInlineInput(Field(type, "A", "B"));

            Assert.IsType<ControlFormItemInputSelection>(input);
        }

        /// <summary>
        /// Verifies that uploads and the workflow state are not edited in a row.
        /// </summary>
        [Theory]
        [InlineData(FieldType.Attachment)]
        [InlineData(FieldType.Avatar)]
        [InlineData(FieldType.Workflow)]
        public void CreateInlineInput_NotInline_IsNull(FieldType type)
        {
            Assert.Null(ObjectFieldSheet.CreateInlineInput(Field(type)));
        }

        /// <summary>
        /// Verifies that the remaining types keep the control of the edit mask.
        /// </summary>
        [Fact]
        public void CreateInlineInput_Tag_IsTagInput()
        {
            Assert.IsType<ControlFormItemInputTag>(ObjectFieldSheet.CreateInlineInput(Field(FieldType.Tag)));
        }

        /// <summary>
        /// Verifies that a stored list reaches a selection with the separator it reads.
        /// </summary>
        [Fact]
        public void CreateInputValue_MultiSelection_SeparatesWithSemicolons()
        {
            var field = Field(FieldType.MultiSelection, "A", "B");
            var input = ObjectFieldSheet.CreateInlineInput(field);

            var value = ObjectFieldSheet.CreateInputValue(input, field, "A, B");

            Assert.Equal("A;B", Assert.IsType<ControlFormInputValueString>(value).Text);
        }

        /// <summary>
        /// Verifies that a boolean is seeded as the option id of the yes/no selection.
        /// </summary>
        [Theory]
        [InlineData("True", "true")]
        [InlineData("false", "false")]
        [InlineData("ja", "false")]
        public void CreateInputValue_Boolean_IsOptionId(string stored, string expected)
        {
            var field = Field(FieldType.Boolean);
            var input = ObjectFieldSheet.CreateInlineInput(field);

            var value = ObjectFieldSheet.CreateInputValue(input, field, stored);

            Assert.Equal(expected, Assert.IsType<ControlFormInputValueString>(value).Text);
        }

        /// <summary>
        /// Verifies that tags reach the tag input as a list.
        /// </summary>
        [Fact]
        public void CreateInputValue_Tag_IsList()
        {
            var field = Field(FieldType.Tag);
            var input = ObjectFieldSheet.CreateInlineInput(field);

            var value = ObjectFieldSheet.CreateInputValue(input, field, "urgent,backend");

            Assert.Equal(["urgent", "backend"], Assert.IsType<ControlFormInputValueStringList>(value).Items);
        }

        /// <summary>
        /// Verifies that a stored value the options do not offer is offered anyway, so the
        /// selection shows it rather than reading as unset.
        /// </summary>
        [Fact]
        public void CreateInlineInput_UnknownStoredOption_IsOffered()
        {
            var input = ObjectFieldSheet.CreateInlineInput(Field(FieldType.Selection, "Hardware", "Software"), "Legacy");

            var ids = Assert.IsType<ControlFormItemInputSelection>(input).Options.Select(x => x.Id).ToList();

            Assert.Equal(["Hardware", "Software", "Legacy"], ids);
        }

        /// <summary>
        /// Verifies that a stored value an offered option already names is not offered twice.
        /// </summary>
        [Fact]
        public void CreateInlineInput_KnownStoredOption_IsNotDoubled()
        {
            var input = ObjectFieldSheet.CreateInlineInput(Field(FieldType.MultiSelection, "A", "B"), "a;B");

            Assert.Equal(2, Assert.IsType<ControlFormItemInputSelection>(input).Options.Count());
        }

        /// <summary>
        /// Verifies that a value a range control cannot hold seeds nothing - the sheet then
        /// shows it as text instead of as an empty range.
        /// </summary>
        [Theory]
        [InlineData(FieldType.DateRange)]
        [InlineData(FieldType.Slider)]
        public void CreateInputValue_UnparseableRange_IsNull(FieldType type)
        {
            var field = Field(type);
            var input = ObjectFieldSheet.CreateInlineInput(field);

            Assert.Null(ObjectFieldSheet.CreateInputValue(input, field, "Availability for DEV-8000"));
        }

        /// <summary>
        /// Verifies that an unset value seeds nothing.
        /// </summary>
        [Fact]
        public void CreateInputValue_Blank_IsNull()
        {
            var field = Field(FieldType.Tag);

            Assert.Null(ObjectFieldSheet.CreateInputValue(ObjectFieldSheet.CreateInlineInput(field), field, " "));
        }

        /// <summary>
        /// Verifies that a rich-text document around an empty paragraph counts as unset.
        /// </summary>
        [Fact]
        public void IsEmpty_EmptyRichText_IsEmpty()
        {
            const string empty = """{"version":1,"doc":{"type":"doc","id":"n1","children":[{"type":"p","id":"n2","children":[]}]}}""";

            Assert.True(ObjectFieldSheet.IsEmpty(Field(FieldType.RichText), empty));
            Assert.False(ObjectFieldSheet.IsEmpty(Field(FieldType.Text), "x"));
        }
    }
}
