using System.Text.Json;
using Form = KleeneStar.Core.WWW.Api._1_.Objects.Form;

namespace KleeneStar.Core.Test.WWW.Api
{
    /// <summary>
    /// Tests <see cref="Form.Answers"/>: a step of the create wizard reloaded for the same class
    /// and template starts from the answers of the payload, a step rendered for another class or
    /// template or not yet rendered starts from the presets.
    /// </summary>
    public class UnitTestObjectWizardAnswers
    {
        private static readonly Guid ClassId = Guid.Parse("8B1C3A44-6C1E-4A7C-9F2D-1E4B5C6D7E8F");
        private static readonly Guid TemplateId = Guid.Parse("1F2E3D4C-5B6A-4978-8695-A4B3C2D1E0F9");

        /// <summary>
        /// Builds a wizard payload as the client posts it.
        /// </summary>
        /// <param name="json">The payload.</param>
        /// <returns>The parsed entries.</returns>
        private static IReadOnlyDictionary<string, JsonElement> Payload(string json)
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        }

        /// <summary>
        /// With the context of the step equal to class and template, every answer except the
        /// static steps' and the markers is returned; a cleared answer stays blank.
        /// </summary>
        [Fact]
        public void Answers_KeepTheEntriesOfTheSameContext()
        {
            var context = Form.Context(ClassId, TemplateId);
            var payload = Payload($$"""
                {
                  "WorkspaceId": "{{Guid.NewGuid()}}",
                  "ClassId": "{{ClassId}}",
                  "TemplateId": "{{TemplateId}}",
                  "IdempotencyKey": "abc",
                  "WizardContextobject": "{{context}}",
                  "WizardContextvalues": "{{context}}",
                  "Summary": "Printer offline",
                  "Description": "",
                  "Confidential": true,
                  "Tags": ["urgent", "printer"],
                  "EstimatedHours": 3
                }
                """);

            var answers = Form.Answers(payload, Form.PartObject, ClassId, TemplateId);

            Assert.Equal("Printer offline", answers["Summary"]);
            Assert.Equal(string.Empty, answers["Description"]);
            Assert.Equal("true", answers["Confidential"]);
            Assert.Equal("urgent;printer", answers["Tags"]);
            Assert.Equal("3", answers["EstimatedHours"]);
            Assert.False(answers.ContainsKey("ClassId"));
            Assert.False(answers.ContainsKey("IdempotencyKey"));
            Assert.False(answers.ContainsKey("WizardContextvalues"));
        }

        /// <summary>
        /// A step rendered for another template, another class or not yet rendered starts from
        /// the presets: no answers.
        /// </summary>
        [Fact]
        public void Answers_AreEmptyForAnotherContext()
        {
            var other = Form.Context(ClassId, Guid.Empty);
            var payload = Payload($$"""
                { "WizardContextobject": "{{other}}", "Summary": "Printer offline" }
                """);

            Assert.Empty(Form.Answers(payload, Form.PartObject, ClassId, TemplateId));
            Assert.Empty(Form.Answers(payload, Form.PartValues, ClassId, Guid.Empty));
            Assert.Empty(Form.Answers(Payload("""{ "Summary": "Printer offline" }"""), Form.PartObject, ClassId, TemplateId));
        }
    }
}
