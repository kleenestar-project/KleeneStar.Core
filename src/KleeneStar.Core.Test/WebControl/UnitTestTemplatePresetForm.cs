using KleeneStar.Core.WebControl;
using KleeneStar.Model.Entities;
using System.Globalization;
using System.Text.Json;

namespace KleeneStar.Core.Test.WebControl
{
    /// <summary>
    /// Provides unit tests for <see cref="TemplatePresetForm"/> and <see cref="TemplateChoice"/>,
    /// the presets step of the template wizard and the template cards of the object wizard.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestTemplatePresetForm
    {
        private static readonly Guid WorkspaceId = Guid.Parse("5A1B2C3D-0000-4000-8000-00000000A001");
        private static readonly Guid IncidentId = Guid.Parse("5A1B2C3D-0000-4000-8000-00000000A002");
        private static readonly Guid ProblemId = Guid.Parse("5A1B2C3D-0000-4000-8000-00000000A003");

        /// <summary>
        /// Seeds a workspace with two classes: an incident with category, priority, flag, due
        /// date and workflow, and a problem with category and impact.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-preset", Name = "Presets" });
            db.Classes.Add(new Class { Id = IncidentId, Name = "Incident", WorkspaceId = WorkspaceId });
            db.Classes.Add(new Class { Id = ProblemId, Name = "Problem", WorkspaceId = WorkspaceId });

            void Field(Guid classId, string name, FieldType type) => db.Fields.Add(new Field
            {
                Id = Guid.NewGuid(),
                Name = name,
                FieldType = type,
                ClassId = classId,
                State = FieldState.Active
            });

            Field(IncidentId, "Category", FieldType.Text);
            Field(IncidentId, "Priority", FieldType.Text);
            Field(IncidentId, "Flag", FieldType.Boolean);
            Field(IncidentId, "Due", FieldType.Date);
            Field(IncidentId, "Status", FieldType.Workflow);
            Field(IncidentId, "Label", FieldType.Color);
            Field(IncidentId, "Stars", FieldType.Rating);
            Field(ProblemId, "Category", FieldType.Text);
            Field(ProblemId, "Impact", FieldType.Text);

            db.SaveChanges();
        }

        /// <summary>
        /// Verifies that the card under the owning class carries the template id and a card under
        /// a further class a distinct value, and that both read back as the template.
        /// </summary>
        [Fact]
        public void TemplateChoice_ValueIsDistinctPerClass_AndParsesBack()
        {
            var templateId = Guid.NewGuid();

            var owner = TemplateChoice.Value(templateId, IncidentId, IncidentId);
            var further = TemplateChoice.Value(templateId, ProblemId, IncidentId);

            Assert.Equal(templateId.ToString(), owner);
            Assert.NotEqual(owner, further);
            Assert.True(TemplateChoice.TryParse(owner, out var a));
            Assert.True(TemplateChoice.TryParse(further, out var b));
            Assert.Equal(templateId, a);
            Assert.Equal(templateId, b);
            Assert.False(TemplateChoice.TryParse("none", out _));
            Assert.False(TemplateChoice.TryParse(null, out _));
        }

        /// <summary>
        /// Verifies that the class selection is read from the tile value, a list and a single id,
        /// in order and without duplicates or foreign entries.
        /// </summary>
        [Fact]
        public void ParseClassIds_ReadsEveryShape()
        {
            Assert.Equal([IncidentId, ProblemId], TemplatePresetForm.ParseClassIds($"{IncidentId};{ProblemId};{IncidentId};nonsense"));
            Assert.Equal([ProblemId, IncidentId], TemplatePresetForm.ParseClassIds(new List<object> { ProblemId.ToString(), IncidentId.ToString() }));
            Assert.Equal([IncidentId], TemplatePresetForm.ParseClassIds(IncidentId.ToString()));
            Assert.Equal([ProblemId], TemplatePresetForm.ParseClassIds(JsonDocument.Parse($"[\"{ProblemId}\"]").RootElement));
            Assert.Empty(TemplatePresetForm.ParseClassIds(null));
        }

        /// <summary>
        /// Verifies that the step offers each field name once and leaves out the workflow.
        /// </summary>
        [Fact]
        public void Fields_OfferEachNameOnce()
        {
            Seed(nameof(Fields_OfferEachNameOnce));

            var fields = TemplatePresetForm.Fields([IncidentId, ProblemId]);

            Assert.Equal(7, fields.Count);
            Assert.Equal(IncidentId, fields["category"].ClassId);
            Assert.False(fields.ContainsKey("Status"));
            Assert.True(fields.ContainsKey("Impact"));
        }

        /// <summary>
        /// Verifies the presets a submitted step stores: answers in storage form, blank answers
        /// and unticked checkboxes preset nothing, a stored preset naming no field of the served
        /// classes is dropped, a system key is kept.
        /// </summary>
        [Fact]
        public void Collect_StoresAnswersInStorageForm()
        {
            Seed(nameof(Collect_StoresAnswersInStorageForm));

            var stored = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Category"] = "Old",
                ["Orphan"] = "x",
                ["Description"] = "Starts like this",
                ["Impact"] = "High"
            };
            var payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["preset_summary"] = "Printer jam",
                ["preset_category"] = "Network",
                ["preset_priority"] = "",
                ["preset_flag"] = false,
                ["preset_due"] = "2026-11-20T10:00:00"
            };

            var json = TemplatePresetForm.Collect(payload, [IncidentId], stored, CultureInfo.InvariantCulture);
            var presets = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            Assert.Equal("Printer jam", presets["Summary"]);
            Assert.Equal("Network", presets["Category"]);
            Assert.Equal("Starts like this", presets["Description"]);
            Assert.StartsWith("2026-11-20T10:00:00", presets["Due"]);
            Assert.False(presets.ContainsKey("Priority"));
            Assert.False(presets.ContainsKey("Flag"));
            Assert.False(presets.ContainsKey("Orphan"));
            Assert.False(presets.ContainsKey("Impact"));
        }

        /// <summary>
        /// Verifies that a field of a further class keeps its stored preset while its input was
        /// not submitted, and takes the answer once it is.
        /// </summary>
        [Fact]
        public void Collect_KeepsStoredPresetOfInputNotSubmitted()
        {
            Seed(nameof(Collect_KeepsStoredPresetOfInputNotSubmitted));

            var stored = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Impact"] = "High" };
            var payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["preset_category"] = "Network" };

            var kept = JsonSerializer.Deserialize<Dictionary<string, string>>(
                TemplatePresetForm.Collect(payload, [IncidentId, ProblemId], stored, CultureInfo.InvariantCulture));

            Assert.Equal("High", kept["Impact"]);

            payload["preset_impact"] = "Low";

            var changed = JsonSerializer.Deserialize<Dictionary<string, string>>(
                TemplatePresetForm.Collect(payload, [IncidentId, ProblemId], stored, CultureInfo.InvariantCulture));

            Assert.Equal("Low", changed["Impact"]);
        }

        /// <summary>
        /// Verifies that the preset of an add-on (<c>{plugin id}:{name}</c>), for which the step
        /// offers no input, is kept as stored, while a stored preset naming no field is dropped.
        /// </summary>
        [Fact]
        public void Collect_KeepsThePresetsOfAddOns()
        {
            Seed(nameof(Collect_KeepsThePresetsOfAddOns));

            var stored = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["kleenestar.calendar:duration"] = "45",
                ["Vanished"] = "gone"
            };
            var payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["preset_category"] = "Network" };

            var kept = JsonSerializer.Deserialize<Dictionary<string, string>>(
                TemplatePresetForm.Collect(payload, [IncidentId], stored, CultureInfo.InvariantCulture));

            Assert.Equal("45", kept["kleenestar.calendar:duration"]);
            Assert.False(kept.ContainsKey("Vanished"));
            Assert.True(TemplatePresetForm.IsAddOnKey("kleenestar.calendar:duration"));
            Assert.False(TemplatePresetForm.IsAddOnKey(":duration"));
            Assert.False(TemplatePresetForm.IsAddOnKey("Duration"));
        }

        /// <summary>
        /// Verifies that a control submitting its starting value presets nothing, unless the
        /// field already carried a preset the control was started from.
        /// </summary>
        [Fact]
        public void Collect_IgnoresUntouchedControls()
        {
            Seed(nameof(Collect_IgnoresUntouchedControls));

            var payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["preset_label"] = "#000000",
                ["preset_stars"] = "0",
                ["preset_category"] = "Network"
            };

            var fresh = JsonSerializer.Deserialize<Dictionary<string, string>>(
                TemplatePresetForm.Collect(payload, [IncidentId], null, CultureInfo.InvariantCulture));

            Assert.False(fresh.ContainsKey("Label"));
            Assert.False(fresh.ContainsKey("Stars"));

            var stored = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Label"] = "#000000" };
            var kept = JsonSerializer.Deserialize<Dictionary<string, string>>(
                TemplatePresetForm.Collect(payload, [IncidentId], stored, CultureInfo.InvariantCulture));

            Assert.Equal("#000000", kept["Label"]);
        }

        /// <summary>
        /// Verifies that a step without any answer stores no presets at all, and that a payload
        /// without preset keys is recognised as one that does not carry the step.
        /// </summary>
        [Fact]
        public void Collect_WithoutAnswersStoresNothing()
        {
            Seed(nameof(Collect_WithoutAnswersStoresNothing));

            var payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["preset_category"] = " " };

            Assert.Null(TemplatePresetForm.Collect(payload, [IncidentId], null, CultureInfo.InvariantCulture));
            Assert.True(TemplatePresetForm.Carries(payload.Keys));
            Assert.False(TemplatePresetForm.Carries(["name", "category"]));
        }
    }
}
