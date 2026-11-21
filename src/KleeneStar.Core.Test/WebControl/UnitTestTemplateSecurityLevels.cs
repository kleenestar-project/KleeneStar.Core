using KleeneStar.Core.WebControl;
using System.Text.Json;

namespace KleeneStar.Core.Test.WebControl
{
    /// <summary>
    /// Tests <see cref="TemplateSecurityLevels"/>, the security level choices of a template.
    /// </summary>
    public class UnitTestTemplateSecurityLevels
    {
        /// <summary>
        /// An unclassified choice is written per class and read back for that class only; a
        /// class without a choice takes its default.
        /// </summary>
        [Fact]
        public void Unclassified_IsAChoicePerClass()
        {
            var classA = Guid.NewGuid();
            var classB = Guid.NewGuid();
            var value = TemplateSecurityLevels.Format(new Dictionary<Guid, Guid?> { [classA] = null });

            Assert.Equal($"none@{classA}", value);

            var presets = new Dictionary<string, string> { [TemplateSecurityLevels.PresetKey] = value };

            Assert.True(TemplateSecurityLevels.TryResolve(presets, classA, out var level));
            Assert.Null(level);
            Assert.False(TemplateSecurityLevels.TryResolve(presets, classB, out _));
            Assert.Equal(Guid.Empty.ToString(), TemplateSecurityLevels.ForClass(presets, classA)[TemplateSecurityLevels.PresetKey]);
            Assert.False(TemplateSecurityLevels.ForClass(presets, classB).ContainsKey(TemplateSecurityLevels.PresetKey));
        }

        /// <summary>
        /// The empty guid as the whole value leaves every class unclassified.
        /// </summary>
        [Fact]
        public void EmptyGuid_LeavesEveryClassUnclassified()
        {
            var presets = new Dictionary<string, string> { [TemplateSecurityLevels.PresetKey] = Guid.Empty.ToString() };

            Assert.True(TemplateSecurityLevels.TryResolve(presets, Guid.NewGuid(), out var level));
            Assert.Null(level);
        }

        /// <summary>
        /// The answers of the wizard step replace the choices of the classes they name: the
        /// default tile removes a choice, the empty guid chooses unclassified; a class the payload
        /// does not name keeps its stored choice.
        /// </summary>
        [Fact]
        public void Collect_TakesTheAnswersAndKeepsTheRest()
        {
            var classA = Guid.NewGuid();
            var classB = Guid.NewGuid();
            var stored = $"none@{classA};none@{classB}";
            var payload = new Dictionary<string, object>
            {
                [TemplateSecurityLevels.FieldPrefix + classA] = TemplateSecurityLevels.DefaultValue
            };

            Assert.Equal($"none@{classB}", TemplateSecurityLevels.Collect(payload, [classA, classB], stored));

            payload[TemplateSecurityLevels.FieldPrefix + classB] = Guid.Empty.ToString();

            Assert.Equal($"none@{classB}", TemplateSecurityLevels.Collect(payload, [classA, classB], null));
            Assert.Null(TemplateSecurityLevels.Collect(new Dictionary<string, object> { [TemplateSecurityLevels.FieldPrefix + classA] = "default" }, [classA], null));
        }

        /// <summary>
        /// The choice is written into the serialized presets beside the others and removed again.
        /// </summary>
        [Fact]
        public void Store_WritesAndRemovesThePreset()
        {
            var presets = TemplateSecurityLevels.Store("""{"Priority":"Low"}""", "none@" + Guid.Empty);
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(presets);

            Assert.Equal("Low", map["Priority"]);
            Assert.True(map.ContainsKey(TemplateSecurityLevels.PresetKey));

            map = JsonSerializer.Deserialize<Dictionary<string, string>>(TemplateSecurityLevels.Store(presets, null));

            Assert.False(map.ContainsKey(TemplateSecurityLevels.PresetKey));
            Assert.Null(TemplateSecurityLevels.Store(null, null));
        }
    }
}
