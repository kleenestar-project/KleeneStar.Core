using KleeneStar.Core.WebManager;
using KleeneStar.Model.Entities;
using KleeneStar.Model.Integrity;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.AuditManager"/> - the
    /// append-only, hash-chained record of what the installation did, and the states replayed
    /// from it.
    /// </summary>
    /// <remarks>
    /// The tests are grouped around the four properties the audit log claims: a stable and
    /// unambiguous order, delta storage that distinguishes the three kinds of change,
    /// reconstructability of a past state, and tamper evidence. A claim of this sort that is not
    /// tested is a claim that will quietly stop being true.
    /// </remarks>
    [Collection("NonParallelTests")]
    public class UnitTestAuditManager
    {
        private static readonly Guid IdentityId = Guid.Parse("C1D2E3F4-A5B6-4C7D-8E9F-0A1B2C3D4E5F");
        private static readonly Guid ClassId = Guid.Parse("D2E3F4A5-B6C7-4D8E-9F0A-1B2C3D4E5F60");
        private static readonly Guid WorkspaceId = Guid.Parse("E3F4A5B6-C7D8-4E9F-A0B1-2C3D4E5F6071");

        /// <summary>
        /// Points the hubs at an isolated database carrying one identity, so an event has
        /// somebody to be attributed to.
        /// </summary>
        /// <param name="connectionString">The isolated database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Identities.Any(x => x.Id == IdentityId))
            {
                db.Identities.Add(new Identity
                {
                    Id = IdentityId,
                    Name = "Erika Mustermann",
                    UserName = "erika",
                    Email = "erika@kleenestar.org",
                    PasswordHash = "$test$"
                });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Builds a class the log can be pointed at.
        /// </summary>
        /// <param name="name">The name the class carries.</param>
        /// <returns>The class.</returns>
        private static Class Sample(string name)
        {
            return new Class
            {
                Id = ClassId,
                Name = name,
                WorkspaceId = WorkspaceId,
                Description = "sample"
            };
        }

        /// <summary>
        /// The sequence is assigned by the store, starts at one and grows without gaps, whatever
        /// the caller put in the event.
        /// </summary>
        [Fact]
        public void Record_AssignsGapFreeSequence()
        {
            Seed(nameof(Record_AssignsGapFreeSequence));

            var first = CoreHub.AuditManager.Record(AuditCategory.Lifecycle, AuditAction.Started, AuditTarget.Installation);
            var second = CoreHub.AuditManager.Record(AuditCategory.Lifecycle, AuditAction.Seeded, AuditTarget.Installation);
            var third = CoreHub.AuditManager.Record(AuditCategory.Lifecycle, AuditAction.Stopped, AuditTarget.Installation);

            Assert.Equal(1, first.Sequence);
            Assert.Equal(2, second.Sequence);
            Assert.Equal(3, third.Sequence);
            Assert.Equal(3, CoreHub.AuditManager.Count);
        }

        /// <summary>
        /// The timestamp is recorded in UTC regardless of the zone the server runs in, so the
        /// order the log is read in does not depend on where it was written.
        /// </summary>
        [Fact]
        public void Record_StoresTimestampInUtc()
        {
            Seed(nameof(Record_StoresTimestampInUtc));

            var before = DateTime.UtcNow.AddSeconds(-1);
            var recorded = CoreHub.AuditManager.Record(AuditCategory.Lifecycle, AuditAction.Started, AuditTarget.Installation);
            var after = DateTime.UtcNow.AddSeconds(1);

            Assert.InRange(recorded.Timestamp, before, after);
            Assert.Equal(recorded.Timestamp, recorded.Timestamp.ToUniversalTime());
        }

        /// <summary>
        /// A creation records every populated attribute as an addition, so the log can replay the
        /// record from this event alone.
        /// </summary>
        [Fact]
        public void RecordChange_Created_DescribesEveryAttributeAsAdded()
        {
            Seed(nameof(RecordChange_Created_DescribesEveryAttributeAsAdded));

            var recorded = CoreHub.AuditManager.RecordChange(AuditCategory.Configuration, AuditAction.Created, Sample("Bug"));

            Assert.NotEmpty(recorded.Deltas);
            Assert.All(recorded.Deltas, x => Assert.Equal(AuditDeltaKind.Added, x.Kind));
            Assert.Equal("Bug", recorded.GetDelta("name").NewValue);
            Assert.Equal(AuditTargetType.Class, recorded.TargetType);
            Assert.Equal(ClassId, recorded.TargetId);
        }

        /// <summary>
        /// A later modification is diffed against what the log already knew, so it states the
        /// change rather than restating the record.
        /// </summary>
        [Fact]
        public void RecordChange_Updated_RecordsOnlyWhatMoved()
        {
            Seed(nameof(RecordChange_Updated_RecordsOnlyWhatMoved));

            CoreHub.AuditManager.RecordChange(AuditCategory.Configuration, AuditAction.Created, Sample("Bug"));

            var updated = Sample("Defect");
            var recorded = CoreHub.AuditManager.RecordChange(AuditCategory.Configuration, AuditAction.Updated, updated);

            var delta = Assert.Single(recorded.Deltas);

            Assert.Equal(AuditDeltaKind.Modified, delta.Kind);
            Assert.Equal("name", delta.Attribute);
            Assert.Equal("Bug", delta.OldValue);
            Assert.Equal("Defect", delta.NewValue);
        }

        /// <summary>
        /// An attribute that is emptied is recorded as a removal, not as a modification to
        /// nothing - the two describe different states and the projection has to be able to tell
        /// them apart.
        /// </summary>
        [Fact]
        public void RecordChange_ClearedAttribute_IsRecordedAsRemoval()
        {
            Seed(nameof(RecordChange_ClearedAttribute_IsRecordedAsRemoval));

            CoreHub.AuditManager.RecordChange(AuditCategory.Configuration, AuditAction.Created, Sample("Bug"));

            var cleared = Sample("Bug");
            cleared.Description = null;

            var recorded = CoreHub.AuditManager.RecordChange(AuditCategory.Configuration, AuditAction.Updated, cleared);

            var delta = Assert.Single(recorded.Deltas);

            Assert.Equal(AuditDeltaKind.Removed, delta.Kind);
            Assert.Equal("description", delta.Attribute);
            Assert.Equal("sample", delta.OldValue);
            Assert.Null(delta.NewValue);
        }

        /// <summary>
        /// Replaying the deltas of a record reproduces the state it held, which is what makes
        /// delta storage sufficient rather than merely compact.
        /// </summary>
        [Fact]
        public void Project_ReplaysTheStateTheRecordHeld()
        {
            Seed(nameof(Project_ReplaysTheStateTheRecordHeld));

            CoreHub.AuditManager.RecordChange(AuditCategory.Configuration, AuditAction.Created, Sample("Bug"));

            var renamed = Sample("Defect");
            renamed.Description = null;

            CoreHub.AuditManager.RecordChange(AuditCategory.Configuration, AuditAction.Updated, renamed);

            var projection = CoreHub.AuditManager.Project(AuditTargetType.Class, ClassId);

            Assert.Equal("Defect", projection.Get("name").Value);
            Assert.Null(projection.Get("description"));
            Assert.Equal(2, projection.EventCount);
        }

        /// <summary>
        /// Replaying up to an earlier position reproduces the state as of that position, which is
        /// what "reconstructable" has to mean.
        /// </summary>
        [Fact]
        public void Project_AtEarlierSequence_ReplaysThePastState()
        {
            Seed(nameof(Project_AtEarlierSequence_ReplaysThePastState));

            var genesis = CoreHub.AuditManager.RecordChange(AuditCategory.Configuration, AuditAction.Created, Sample("Bug"));

            CoreHub.AuditManager.RecordChange(AuditCategory.Configuration, AuditAction.Updated, Sample("Defect"));

            var past = CoreHub.AuditManager.Project(AuditTargetType.Class, ClassId, genesis.Sequence);
            var present = CoreHub.AuditManager.Project(AuditTargetType.Class, ClassId);

            Assert.Equal("Bug", past.Get("name").Value);
            Assert.Equal("Defect", present.Get("name").Value);
        }

        /// <summary>
        /// The events of one action share a correlation, and each names the one before it as its
        /// cause, so a decision can be recovered from its consequences.
        /// </summary>
        [Fact]
        public void BeginActivity_CorrelatesTheEventsOfOneAction()
        {
            Seed(nameof(BeginActivity_CorrelatesTheEventsOfOneAction));

            AuditEvent first;
            AuditEvent second;

            using (var activity = CoreHub.AuditManager.BeginActivity(AuditOrigin.User, IdentityId, null, "203.0.113.7"))
            {
                first = CoreHub.AuditManager.RecordChange(AuditCategory.Configuration, AuditAction.Created, Sample("Bug"));
                second = CoreHub.AuditManager.Record(AuditCategory.Configuration, AuditAction.Deleted, AuditTarget.Describe(Sample("Bug")));

                Assert.Equal(activity.CorrelationId, first.CorrelationId);
            }

            Assert.Equal(first.CorrelationId, second.CorrelationId);
            Assert.Equal(first.Id, second.CausationId);
            Assert.Equal(AuditOrigin.User, first.Origin);
            Assert.Equal(IdentityId, first.ActorId);
            Assert.Equal("Erika Mustermann", first.ActorName);
            Assert.Equal("203.0.113.7", first.ClientAddress);

            var activityEvents = CoreHub.AuditManager.GetActivity(first.CorrelationId).ToList();

            Assert.Equal(2, activityEvents.Count);
        }

        /// <summary>
        /// A nested activity joins the open one rather than starting a second, so a manager may
        /// open one without knowing what its caller did.
        /// </summary>
        [Fact]
        public void BeginActivity_Nested_JoinsTheOpenOne()
        {
            Seed(nameof(BeginActivity_Nested_JoinsTheOpenOne));

            using var outer = CoreHub.AuditManager.BeginActivity(AuditOrigin.External, Guid.Empty, "rest.api.v1");

            AuditEvent inner;

            using (CoreHub.AuditManager.BeginActivity(AuditOrigin.System, IdentityId))
            {
                inner = CoreHub.AuditManager.Record(AuditCategory.Integration, AuditAction.Invoked, AuditTarget.None);
            }

            var after = CoreHub.AuditManager.Record(AuditCategory.Integration, AuditAction.Invoked, AuditTarget.None);

            // the inner call filled in the actor the outer one did not know, but did not relabel
            // the origin the outer one established
            Assert.Equal(outer.CorrelationId, inner.CorrelationId);
            Assert.Equal(outer.CorrelationId, after.CorrelationId);
            Assert.Equal(AuditOrigin.External, inner.Origin);
            Assert.Equal(IdentityId, inner.ActorId);
        }

        /// <summary>
        /// A secret leaves a delta saying that it changed, and no delta saying what it changed
        /// to.
        /// </summary>
        [Fact]
        public void RecordChange_RedactedAttribute_RecordsTheChangeButNotTheValue()
        {
            Seed(nameof(RecordChange_RedactedAttribute_RecordsTheChangeButNotTheValue));

            var identity = new Identity
            {
                Id = IdentityId,
                Name = "Erika Mustermann",
                UserName = "erika",
                PasswordHash = "$argon2id$v=19$m=65536,t=3,p=4$verysecret"
            };

            var recorded = CoreHub.AuditManager.RecordChange(AuditCategory.Identity, AuditAction.Created, identity);

            var delta = recorded.GetDelta("passwordhash");

            Assert.NotNull(delta);
            Assert.Equal(AuditValueKind.Redacted, delta.ValueKind);
            Assert.Equal(AuditValueKindExtensions.RedactedMarker, delta.NewValue);
            Assert.DoesNotContain(recorded.Deltas, x => (x.NewValue ?? string.Empty).Contains("verysecret"));
        }

        /// <summary>
        /// A freshly written log verifies, and its head hash is the value an operator keeps to
        /// pin everything before it.
        /// </summary>
        [Fact]
        public void Verify_UntouchedLog_IsIntact()
        {
            Seed(nameof(Verify_UntouchedLog_IsIntact));

            CoreHub.AuditManager.RecordChange(AuditCategory.Configuration, AuditAction.Created, Sample("Bug"));
            CoreHub.AuditManager.RecordChange(AuditCategory.Configuration, AuditAction.Updated, Sample("Defect"));
            CoreHub.AuditManager.Record(AuditCategory.Lifecycle, AuditAction.Stopped, AuditTarget.Installation);

            var verification = CoreHub.AuditManager.Verify();

            Assert.True(verification.IsIntact);
            Assert.Equal(3, verification.Checked);
            Assert.Equal(1, verification.FromSequence);
            Assert.Equal(3, verification.ToSequence);
            Assert.False(string.IsNullOrEmpty(verification.HeadHash));
        }

        /// <summary>
        /// Editing a stored event breaks the chain, and the verification names the position it
        /// broke at.
        /// </summary>
        /// <remarks>
        /// The edit is made directly against the store, bypassing the manager entirely - which
        /// is the only way it could ever happen, and therefore the only meaningful way to test
        /// that it is detected.
        /// </remarks>
        [Fact]
        public void Verify_TamperedEvent_ReportsWhereTheChainBroke()
        {
            var connection = nameof(Verify_TamperedEvent_ReportsWhereTheChainBroke);

            Seed(connection);

            CoreHub.AuditManager.RecordChange(AuditCategory.Configuration, AuditAction.Created, Sample("Bug"));
            CoreHub.AuditManager.RecordChange(AuditCategory.Configuration, AuditAction.Updated, Sample("Defect"));
            CoreHub.AuditManager.Record(AuditCategory.Lifecycle, AuditAction.Stopped, AuditTarget.Installation);

            using (var db = CoreHubFixture.CreateDbContext(connection))
            {
                var forged = db.AuditEvents.First(x => x.Sequence == 2);
                forged.ActorName = "somebody else";

                db.SaveChanges();
            }

            var verification = CoreHub.AuditManager.Verify();

            Assert.False(verification.IsIntact);
            Assert.Equal(2, verification.BrokenAt);
        }

        /// <summary>
        /// Deleting an event leaves a gap in the sequence, which the verification reports even
        /// when the surviving rows still hash correctly among themselves.
        /// </summary>
        [Fact]
        public void Verify_DeletedEvent_ReportsTheGap()
        {
            var connection = nameof(Verify_DeletedEvent_ReportsTheGap);

            Seed(connection);

            CoreHub.AuditManager.Record(AuditCategory.Lifecycle, AuditAction.Started, AuditTarget.Installation);
            CoreHub.AuditManager.Record(AuditCategory.Lifecycle, AuditAction.Seeded, AuditTarget.Installation);
            CoreHub.AuditManager.Record(AuditCategory.Lifecycle, AuditAction.Stopped, AuditTarget.Installation);

            using (var db = CoreHubFixture.CreateDbContext(connection))
            {
                db.AuditEvents.RemoveRange(db.AuditEvents.Where(x => x.Sequence == 2));
                db.SaveChanges();
            }

            var verification = CoreHub.AuditManager.Verify();

            Assert.False(verification.IsIntact);
            Assert.Contains(2L, verification.MissingSequences);
        }

        /// <summary>
        /// The seal covers the deltas as well as the event, so a value rewritten underneath an
        /// otherwise untouched event is still detected.
        /// </summary>
        [Fact]
        public void Verify_TamperedDelta_BreaksTheSeal()
        {
            var connection = nameof(Verify_TamperedDelta_BreaksTheSeal);

            Seed(connection);

            var recorded = CoreHub.AuditManager.RecordChange(AuditCategory.Configuration, AuditAction.Created, Sample("Bug"));

            Assert.True(AuditSeal.Verify(recorded, recorded.PreviousHash));

            using (var db = CoreHubFixture.CreateDbContext(connection))
            {
                var delta = db.AuditDeltas.First(x => x.Attribute == "name");
                delta.NewValue = "Feature";

                db.SaveChanges();
            }

            var reread = CoreHub.AuditManager.GetEvent(recorded.Id);

            Assert.False(AuditSeal.Verify(reread, reread.PreviousHash));
        }

        /// <summary>
        /// The trail of a record survives the record, which is the case a forensic reader needs
        /// it in.
        /// </summary>
        [Fact]
        public void GetTrail_SurvivesTheDeletionOfItsSubject()
        {
            Seed(nameof(GetTrail_SurvivesTheDeletionOfItsSubject));

            CoreHub.AuditManager.RecordChange(AuditCategory.Configuration, AuditAction.Created, Sample("Bug"));
            CoreHub.AuditManager.RecordChange(AuditCategory.Configuration, AuditAction.Deleted, Sample("Bug"));

            var trail = CoreHub.AuditManager.GetTrail(AuditTargetType.Class, ClassId).ToList();

            Assert.Equal(2, trail.Count);
            Assert.Equal(AuditAction.Created, trail[0].Action);
            Assert.Equal(AuditAction.Deleted, trail[1].Action);
            Assert.Equal("Bug", trail[1].TargetKey);
            Assert.True(CoreHub.AuditManager.Project(AuditTargetType.Class, ClassId).IsDeleted);
        }

        /// <summary>
        /// Pruning removes the events before the horizon and records that it did, so the gap it
        /// leaves is accounted for by the log rather than merely present in it.
        /// </summary>
        [Fact]
        public void Prune_RecordsTheRemovalItPerformed()
        {
            Seed(nameof(Prune_RecordsTheRemovalItPerformed));

            CoreHub.AuditManager.Record(AuditCategory.Lifecycle, AuditAction.Started, AuditTarget.Installation);
            CoreHub.AuditManager.Record(AuditCategory.Lifecycle, AuditAction.Seeded, AuditTarget.Installation);

            var removed = CoreHub.AuditManager.Prune(DateTime.UtcNow.AddSeconds(1), IdentityId);

            Assert.Equal(2, removed);

            var remaining = CoreHub.AuditManager.Events.ToList();
            var marker = Assert.Single(remaining);

            Assert.Equal(AuditAction.Pruned, marker.Action);
            Assert.Equal(AuditSeverity.Critical, marker.Severity);
            Assert.Equal("2", marker.GetDelta("removed").NewValue);
            Assert.False(string.IsNullOrEmpty(marker.GetDelta("lasthash").NewValue));
        }

        /// <summary>
        /// A configuration change reaches the log without the manager that made it knowing the
        /// log exists, which is the property the central subscription buys.
        /// </summary>
        [Fact]
        public void Connect_RecordsConfigurationChangesWithoutTheManagerKnowing()
        {
            var connection = nameof(Connect_RecordsConfigurationChangesWithoutTheManagerKnowing);

            Seed(connection);

            using (var db = CoreHubFixture.CreateDbContext(connection))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-audit", Name = "workspace" });
                db.SaveChanges();
            }

            CoreHub.AuditManager.Connect();

            CoreHub.ClassManager.Add(Sample("Bug"));

            var stored = CoreHub.ClassManager.GetClass(ClassId);
            stored.Name = "Defect";

            CoreHub.ClassManager.Update(stored);
            CoreHub.ClassManager.Remove(ClassId);

            var trail = CoreHub.AuditManager.GetTrail(AuditTargetType.Class, ClassId).ToList();

            Assert.Equal(3, trail.Count);
            Assert.All(trail, x => Assert.Equal(AuditCategory.Configuration, x.Category));
            Assert.Equal(AuditAction.Created, trail[0].Action);
            Assert.Equal(AuditAction.Updated, trail[1].Action);
            Assert.Equal(AuditAction.Deleted, trail[2].Action);

            // a schema deletion is the change a later investigation has to be able to find
            Assert.Equal(AuditSeverity.Notice, trail[0].Severity);
            Assert.Equal(AuditSeverity.Critical, trail[2].Severity);

            var renamed = Assert.Single(trail[1].Deltas);

            Assert.Equal(AuditDeltaKind.Modified, renamed.Kind);
            Assert.Equal("Bug", renamed.OldValue);
            Assert.Equal("Defect", renamed.NewValue);

            Assert.True(CoreHub.AuditManager.Verify().IsIntact);
        }

        /// <summary>
        /// An object mutation reaches the log through its commit, carrying the exact before and
        /// after of the attribute and the revision the object reached.
        /// </summary>
        [Fact]
        public void Commit_IsBridgedIntoTheLogWithItsRevision()
        {
            var connection = nameof(Commit_IsBridgedIntoTheLogWithItsRevision);

            Seed(connection);

            var objectId = Guid.Parse("F4A5B6C7-D8E9-4F0A-B1C2-3D4E5F607182");
            var fieldId = Guid.Parse("A5B6C7D8-E9F0-4A1B-C2D3-4E5F60718293");

            using (var db = CoreHubFixture.CreateDbContext(connection))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-audit", Name = "workspace" });
                db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
                db.Fields.Add(new Field { Id = fieldId, Name = "Severity", ClassId = ClassId });
                db.Objects.Add(new Model.Entities.Object
                {
                    Id = objectId,
                    Key = "INC-00001",
                    Summary = "VPN down",
                    WorkspaceId = WorkspaceId,
                    ClassId = ClassId,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                });

                db.SaveChanges();
            }

            CoreHub.AuditManager.Connect();

            CoreHub.ValueManager.Add(new Value { ObjectId = objectId, FieldId = fieldId, Data = "high" });

            var trail = CoreHub.AuditManager.GetTrail(AuditTargetType.Object, objectId).ToList();
            var recorded = Assert.Single(trail);

            Assert.Equal(AuditCategory.Content, recorded.Category);
            Assert.Equal("INC-00001", recorded.TargetKey);
            Assert.Equal(1, recorded.TargetRevision);

            var delta = Assert.Single(recorded.Deltas);

            Assert.Equal(AuditDeltaKind.Added, delta.Kind);
            Assert.Equal("Severity", delta.Attribute);
            Assert.Equal(fieldId, delta.AttributeId);
            Assert.Equal("high", delta.NewValue);
        }
    }
}
