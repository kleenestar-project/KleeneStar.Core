using KleeneStar.Model.Entities;
using System.Reflection;
using WebExpress.WebApp.WebRestApi;
using ObjectEntity = KleeneStar.Model.Entities.Object;
using TemplateEntity = KleeneStar.Model.Entities.Template;

namespace KleeneStar.Core.Test.WWW.Api.Objects
{
    /// <summary>
    /// Tests how the presets of a template reach the object created from it. The wizard posts
    /// every input of the create form, so a field the form could not pre-fill arrives as an
    /// empty string - and an empty answer used to shadow the preset, leaving the object without
    /// the very value the template was picked for. A blank is not an answer; a value is. The
    /// summary and the description are columns of the object row rather than value rows, so a
    /// preset on them takes a path of its own.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectTemplatePresets
    {
        private static readonly Guid WorkspaceId = Guid.Parse("E5E5E5E5-0000-4000-8000-000000000001");
        private static readonly Guid ClassId = Guid.Parse("E5E5E5E5-0000-4000-8000-000000000002");
        private static readonly Guid PriorityFieldId = Guid.Parse("E5E5E5E5-0000-4000-8000-000000000003");
        private static readonly Guid ImpactFieldId = Guid.Parse("E5E5E5E5-0000-4000-8000-000000000004");
        private static readonly Guid SynopsisFieldId = Guid.Parse("E5E5E5E5-0000-4000-8000-000000000005");
        private static readonly Guid TemplateId = Guid.Parse("E5E5E5E5-0000-4000-8000-000000000006");
        private static readonly Guid ArchivedTemplateId = Guid.Parse("E5E5E5E5-0000-4000-8000-000000000007");
        private static readonly Guid ObjectId = Guid.Parse("E5E5E5E5-0000-4000-8000-000000000008");

        /// <summary>
        /// The document the prose editor submits for a field nobody wrote into.
        /// </summary>
        private const string EmptyDocument = """{"version":1,"doc":{"type":"doc","id":"n1","attrs":{},"children":[{"type":"row","id":"n4","attrs":{},"children":[{"type":"region","id":"n3","attrs":{"weight":1},"children":[{"type":"p","id":"n2","attrs":{},"children":[]}]}]}]}}""";

        /// <summary>
        /// The same document once somebody typed into it.
        /// </summary>
        private const string WrittenDocument = """{"version":1,"doc":{"type":"doc","id":"n1","attrs":{},"children":[{"type":"p","id":"n2","attrs":{},"children":[{"type":"text","text":"Steps to reproduce","marks":{}}]}]}}""";

        /// <summary>
        /// Seeds one class with a priority, a selection and a prose field, a template
        /// presetting all three plus the description, an archived template, and one object to
        /// apply the presets to.
        /// </summary>
        /// <param name="database">The name of the isolated in-memory database.</param>
        private static void Seed(string database)
        {
            CoreHubFixture.Initialize(database);

            using var db = CoreHubFixture.CreateDbContext(database);

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "TPL", Name = "Templates" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
            db.Fields.AddRange(
                new Field { Id = PriorityFieldId, Name = "Priority", ClassId = ClassId, FieldType = FieldType.Priority },
                new Field { Id = ImpactFieldId, Name = "Impact", ClassId = ClassId, FieldType = FieldType.Selection, Options = ["Low", "Medium", "High"] },
                new Field { Id = SynopsisFieldId, Name = "Synopsis", ClassId = ClassId, FieldType = FieldType.RichText });
            db.Templates.AddRange(
                new TemplateEntity(TemplateId)
                {
                    Name = "Software Issue",
                    ClassId = ClassId,
                    State = TemplateState.Active,
                    Presets = """{"Priority":"P3 - Moderate","Impact":"Medium","Synopsis":"Describe the symptom.","Description":"What happened, and what should have happened?"}"""
                },
                new TemplateEntity(ArchivedTemplateId)
                {
                    Name = "Retired",
                    ClassId = ClassId,
                    State = TemplateState.Archived,
                    Presets = """{"Priority":"P1 - Critical"}"""
                });
            db.Objects.Add(new ObjectEntity(ObjectId)
            {
                Key = "TPL-1",
                Summary = "Login fails",
                WorkspaceId = WorkspaceId,
                ClassId = ClassId,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            });
            db.SaveChanges();
        }

        /// <summary>
        /// Builds a payload the way the form parser hands it over: keys lower-cased.
        /// </summary>
        /// <param name="entries">The payload entries.</param>
        /// <returns>The payload, or null when no entries are given.</returns>
        private static RestApiCrudFormData Payload(params (string Key, object Value)[] entries)
        {
            if (entries.Length == 0)
            {
                return null;
            }

            var payload = new RestApiCrudFormData();

            foreach (var (key, value) in entries)
            {
                payload[key.ToLowerInvariant()] = value;
            }

            return payload;
        }

        /// <summary>
        /// Invokes a private static helper of the object endpoint.
        /// </summary>
        /// <param name="name">The name of the helper.</param>
        /// <param name="arguments">Its arguments.</param>
        /// <returns>What the helper returned.</returns>
        private static object Invoke(string name, params object[] arguments)
        {
            var method = typeof(global::KleeneStar.Core.WWW.Api._1_.Objects.Index)
                .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            return method!.Invoke(null, arguments);
        }

        /// <summary>
        /// Reads a value row of the seeded object back.
        /// </summary>
        /// <param name="fieldId">The field.</param>
        /// <returns>The stored data, or null when the object carries no value for the field.</returns>
        private static string Stored(Guid fieldId)
        {
            return CoreHub.ValueManager.GetValue(ObjectId, fieldId)?.Data;
        }

        /// <summary>
        /// Loads the seeded object, as the endpoint would hold it while creating.
        /// </summary>
        /// <param name="database">The name of the isolated in-memory database.</param>
        /// <returns>The object.</returns>
        private static ObjectEntity Load(string database)
        {
            using var db = CoreHubFixture.CreateDbContext(database);

            return db.Objects.Single(x => x.Id == ObjectId);
        }

        /// <summary>
        /// Without a payload - a child of a composite template - every preset lands.
        /// </summary>
        [Fact]
        public void ApplyPresetValues_WithoutPayload_WritesEveryPreset()
        {
            Seed(nameof(ApplyPresetValues_WithoutPayload_WritesEveryPreset));

            Invoke("ApplyPresetValues", Load(nameof(ApplyPresetValues_WithoutPayload_WritesEveryPreset)), CoreHub.TemplateManager.GetPresets(TemplateId), null);

            Assert.Equal("P3 - Moderate", Stored(PriorityFieldId));
            Assert.Equal("Medium", Stored(ImpactFieldId));
            Assert.Equal("Describe the symptom.", Stored(SynopsisFieldId));
        }

        /// <summary>
        /// A field the caller answered keeps the answer; the preset does not overrule it.
        /// </summary>
        [Fact]
        public void ApplyPresetValues_AnsweredField_KeepsTheAnswer()
        {
            Seed(nameof(ApplyPresetValues_AnsweredField_KeepsTheAnswer));

            CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = ImpactFieldId, Data = "High" });

            Invoke("ApplyPresetValues", Load(nameof(ApplyPresetValues_AnsweredField_KeepsTheAnswer)), CoreHub.TemplateManager.GetPresets(TemplateId), Payload(("Impact", "High")));

            Assert.Equal("High", Stored(ImpactFieldId));
            Assert.Equal("P3 - Moderate", Stored(PriorityFieldId));
        }

        /// <summary>
        /// A field the payload merely mentions - the empty string the wizard posts for an input
        /// the form could not pre-fill - is not an answer, and the preset lands.
        /// </summary>
        [Fact]
        public void ApplyPresetValues_BlankAnswer_WritesThePreset()
        {
            Seed(nameof(ApplyPresetValues_BlankAnswer_WritesThePreset));

            Invoke("ApplyPresetValues", Load(nameof(ApplyPresetValues_BlankAnswer_WritesThePreset)), CoreHub.TemplateManager.GetPresets(TemplateId), Payload(("Priority", ""), ("Impact", "   "), ("Synopsis", null)));

            Assert.Equal("P3 - Moderate", Stored(PriorityFieldId));
            Assert.Equal("Medium", Stored(ImpactFieldId));
            Assert.Equal("Describe the symptom.", Stored(SynopsisFieldId));
        }

        /// <summary>
        /// The empty document the prose editor submits for a field nobody wrote into is blank
        /// too, whereas a document with text is an answer.
        /// </summary>
        [Fact]
        public void ApplyPresetValues_EditorDocument_BlankUnlessWritten()
        {
            Seed(nameof(ApplyPresetValues_EditorDocument_BlankUnlessWritten));

            Invoke("ApplyPresetValues", Load(nameof(ApplyPresetValues_EditorDocument_BlankUnlessWritten)), CoreHub.TemplateManager.GetPresets(TemplateId), Payload(("Synopsis", EmptyDocument)));

            Assert.Equal("Describe the symptom.", Stored(SynopsisFieldId));

            Seed(nameof(ApplyPresetValues_EditorDocument_BlankUnlessWritten) + "2");

            Invoke("ApplyPresetValues", Load(nameof(ApplyPresetValues_EditorDocument_BlankUnlessWritten) + "2"), CoreHub.TemplateManager.GetPresets(TemplateId), Payload(("Synopsis", WrittenDocument)));

            Assert.Null(Stored(SynopsisFieldId));
        }

        /// <summary>
        /// A preset on the description reaches the object row when the caller left the
        /// description blank - also when "blank" is the editor's empty document - and stays
        /// away from one the caller wrote.
        /// </summary>
        [Fact]
        public void ApplyPresetProperties_Description_FillsBlankOnly()
        {
            Seed(nameof(ApplyPresetProperties_Description_FillsBlankOnly));

            var presets = CoreHub.TemplateManager.GetPresets(TemplateId);

            var untouched = new ObjectEntity { Summary = "Login fails", Description = null };
            Invoke("ApplyPresetProperties", untouched, presets);
            Assert.Equal("What happened, and what should have happened?", untouched.Description);
            Assert.Equal("Login fails", untouched.Summary);

            var emptied = new ObjectEntity { Summary = "Login fails", Description = EmptyDocument };
            Invoke("ApplyPresetProperties", emptied, presets);
            Assert.Equal("What happened, and what should have happened?", emptied.Description);

            var written = new ObjectEntity { Summary = "Login fails", Description = WrittenDocument };
            Invoke("ApplyPresetProperties", written, presets);
            Assert.Equal(WrittenDocument, written.Description);
        }

        /// <summary>
        /// A preset on the description is not written as a value row - the summary and the
        /// description are columns of the object, and a value row under their names would
        /// be a second, disagreeing copy.
        /// </summary>
        [Fact]
        public void ApplyPresetValues_Description_IsNotAValueRow()
        {
            Seed(nameof(ApplyPresetValues_Description_IsNotAValueRow));

            Invoke("ApplyPresetValues", Load(nameof(ApplyPresetValues_Description_IsNotAValueRow)), CoreHub.TemplateManager.GetPresets(TemplateId), null);

            Assert.DoesNotContain(CoreHub.ValueManager.GetValues(ObjectId), x => x.FieldId != PriorityFieldId && x.FieldId != ImpactFieldId && x.FieldId != SynopsisFieldId);
        }

        /// <summary>
        /// The payload names the template to apply: an active one resolves, an archived one and
        /// the wizard's "no template" token do not.
        /// </summary>
        [Fact]
        public void ResolveTemplate_ActiveOnly()
        {
            Seed(nameof(ResolveTemplate_ActiveOnly));

            var active = Assert.IsType<TemplateEntity>(Invoke("ResolveTemplate", Payload(("TemplateId", TemplateId.ToString()))));
            Assert.Equal(TemplateId, active.Id);

            Assert.Null(Invoke("ResolveTemplate", Payload(("TemplateId", ArchivedTemplateId.ToString()))));
            Assert.Null(Invoke("ResolveTemplate", Payload(("TemplateId", "none"))));
            Assert.Null(Invoke("ResolveTemplate", Payload(("Summary", "no template named"))));
        }
    }
}
