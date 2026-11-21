using KleeneStar.Core.WebManager;
using KleeneStar.Model.Entities;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.CommitManager"/> — the
    /// append-only history of an object and the states replayed from it.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestCommitManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("6A1F2D30-8C4B-4E19-9F73-2B5C7D8E0A11");
        private static readonly Guid ClassId = Guid.Parse("7B2E3C41-9D5A-4F2A-8E64-3C6D8E9F1B22");
        private static readonly Guid ObjectId = Guid.Parse("8C3D4B52-AE6B-4A3B-9D55-4D7E9F0A2C33");
        private static readonly Guid SeverityFieldId = Guid.Parse("9D4C5A63-BF7C-4B4C-AE46-5E8F0A1B3D44");
        private static readonly Guid OwnerFieldId = Guid.Parse("AE5B6974-C08D-4C5D-BF37-6F901B2C4E55");
        private static readonly Guid IdentityId = Guid.Parse("BF6A7A85-D19E-4D6E-C028-70A12C3D5F66");

        /// <summary>
        /// Points the hubs at an isolated database carrying one workspace, one class with two
        /// fields, one identity, and one object with no history yet.
        /// </summary>
        /// <param name="connectionString">The isolated database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-cm", Name = "workspace" });
            }

            if (!db.Classes.Any(x => x.Id == ClassId))
            {
                db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
            }

            if (!db.Fields.Any(x => x.Id == SeverityFieldId))
            {
                db.Fields.Add(new Field { Id = SeverityFieldId, Name = "Severity", ClassId = ClassId });
            }

            if (!db.Fields.Any(x => x.Id == OwnerFieldId))
            {
                db.Fields.Add(new Field { Id = OwnerFieldId, Name = "Owner", ClassId = ClassId });
            }

            if (!db.Identities.Any(x => x.Id == IdentityId))
            {
                db.Identities.Add(new Identity { Id = IdentityId, Name = "Erika Mustermann", Email = "erika@kleenestar.org", PasswordHash = "$test$" });
            }

            if (!db.Objects.Any(x => x.Id == ObjectId))
            {
                db.Objects.Add(new ObjectEntity
                {
                    Id = ObjectId,
                    Key = "INC-00123",
                    Summary = "VPN connection disrupted",
                    WorkspaceId = WorkspaceId,
                    ClassId = ClassId,
                    CreatorId = IdentityId,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// A value written outside any scope still lands in the history, as a commit of its own.
        /// </summary>
        [Fact]
        public void ValueWrite_WithoutScope_AppendsCommit()
        {
            Seed(nameof(ValueWrite_WithoutScope_AppendsCommit));

            CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = SeverityFieldId, Data = "high" });

            var history = CoreHub.CommitManager.GetHistory(ObjectId).ToList();

            Assert.Single(history);
            Assert.Equal(1, history[0].Number);
            Assert.Equal(CommitType.Updated, history[0].Type);

            var change = Assert.Single(history[0].Changes);
            Assert.Equal("Severity", change.Name);
            Assert.Null(change.OldValue);
            Assert.Equal("high", change.NewValue);
        }

        /// <summary>
        /// A value written outside a scope reaches the store, because the commit that records it
        /// carries it there.
        /// </summary>
        [Fact]
        public void ValueWrite_WithoutScope_PersistsValue()
        {
            Seed(nameof(ValueWrite_WithoutScope_PersistsValue));

            CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = SeverityFieldId, Data = "high" });

            Assert.Equal("high", CoreHub.ValueManager.GetValue(ObjectId, SeverityFieldId)?.Data);
        }

        /// <summary>
        /// Several writes inside one scope collapse into a single commit — one action, one entry.
        /// </summary>
        [Fact]
        public void ValueWrites_InsideScope_CollapseIntoOneCommit()
        {
            Seed(nameof(ValueWrites_InsideScope_CollapseIntoOneCommit));

            using (CoreHub.CommitManager.BeginCommit(ObjectId, CommitType.Updated, IdentityId, "one edit"))
            {
                CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = SeverityFieldId, Data = "high" });
                CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = OwnerFieldId, Data = "Max Power" });
            }

            var history = CoreHub.CommitManager.GetHistory(ObjectId).ToList();

            Assert.Single(history);
            Assert.Equal(2, history[0].Changes.Count);
            Assert.Equal("one edit", history[0].Message);
            Assert.Equal("Erika Mustermann", history[0].CreatedByName);
        }

        /// <summary>
        /// A write staged by an open scope is readable through the manager before the scope
        /// closes, so code that writes and then reads inside one action sees its own change.
        /// </summary>
        [Fact]
        public void ValueRead_InsideScope_SeesStagedWrite()
        {
            Seed(nameof(ValueRead_InsideScope_SeesStagedWrite));

            using (CoreHub.CommitManager.BeginCommit(ObjectId, CommitType.Updated, IdentityId))
            {
                CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = SeverityFieldId, Data = "high" });

                Assert.Equal("high", CoreHub.ValueManager.GetValue(ObjectId, SeverityFieldId)?.Data);
                Assert.Single(CoreHub.ValueManager.GetValues(ObjectId));
            }
        }

        /// <summary>
        /// An aborted scope writes neither the commit nor the values it staged: the current state
        /// and the head of the chain stay in step even when an operation gives up half way.
        /// </summary>
        [Fact]
        public void AbortedScope_WritesNeitherCommitNorValue()
        {
            Seed(nameof(AbortedScope_WritesNeitherCommitNorValue));

            using (var scope = CoreHub.CommitManager.BeginCommit(ObjectId, CommitType.Updated, IdentityId))
            {
                CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = SeverityFieldId, Data = "high" });

                scope.Abort();
            }

            Assert.Empty(CoreHub.CommitManager.GetHistory(ObjectId));
            Assert.Null(CoreHub.ValueManager.GetValue(ObjectId, SeverityFieldId));
        }

        /// <summary>
        /// Nested scopes for the same object join the outer one, so a manager may open a scope
        /// without knowing whether its caller already did.
        /// </summary>
        [Fact]
        public void NestedScope_JoinsOuterCommit()
        {
            Seed(nameof(NestedScope_JoinsOuterCommit));

            using (CoreHub.CommitManager.BeginCommit(ObjectId, CommitType.Updated, IdentityId))
            {
                CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = SeverityFieldId, Data = "high" });

                using (CoreHub.CommitManager.BeginCommit(ObjectId, CommitType.Transitioned, IdentityId))
                {
                    CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = OwnerFieldId, Data = "Max Power" });
                }

                Assert.Empty(CoreHub.CommitManager.GetHistory(ObjectId));
            }

            var history = CoreHub.CommitManager.GetHistory(ObjectId).ToList();

            Assert.Single(history);

            // the inner caller knew more about what happened than the outer one, so its type wins
            Assert.Equal(CommitType.Transitioned, history[0].Type);
        }

        /// <summary>
        /// A write that carries the payload the field already holds is not a change, and does not
        /// appear in the history.
        /// </summary>
        [Fact]
        public void UnchangedWrite_AppendsNoCommit()
        {
            Seed(nameof(UnchangedWrite_AppendsNoCommit));

            CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = SeverityFieldId, Data = "high" });

            var existing = CoreHub.ValueManager.GetValue(ObjectId, SeverityFieldId);
            CoreHub.ValueManager.Update(existing);

            Assert.Single(CoreHub.CommitManager.GetHistory(ObjectId));
        }

        /// <summary>
        /// The chain numbers itself consecutively and each commit points at its predecessor.
        /// </summary>
        [Fact]
        public void Chain_NumbersConsecutively_AndLinksToPredecessor()
        {
            Seed(nameof(Chain_NumbersConsecutively_AndLinksToPredecessor));

            Write("low");
            Write("medium");
            Write("high");

            var history = CoreHub.CommitManager.GetHistory(ObjectId).ToList();

            Assert.Equal([3, 2, 1], history.Select(x => x.Number));
            Assert.Null(history[^1].ParentId);
            Assert.Equal(history[1].Id, history[0].ParentId);
            Assert.Equal(history[2].Id, history[1].ParentId);
        }

        /// <summary>
        /// Replaying the chain up to a revision produces the field set the object carried then,
        /// including the fields that revision did not touch.
        /// </summary>
        [Fact]
        public void GetStateAt_ReplaysFieldsFromEarlierCommits()
        {
            Seed(nameof(GetStateAt_ReplaysFieldsFromEarlierCommits));

            using (CoreHub.CommitManager.BeginCommit(ObjectId, CommitType.Updated, IdentityId))
            {
                CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = SeverityFieldId, Data = "low" });
                CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = OwnerFieldId, Data = "Max Power" });
            }

            Write("high");

            var state = CoreHub.CommitManager.GetStateAt(ObjectId, 2);

            Assert.NotNull(state);
            Assert.True(state.IsHead);
            Assert.Equal("INC-00123#2", state.Reference);
            Assert.Equal("high", state.GetField("Severity")?.Value);

            // the second commit never mentioned the owner; the replay carries it forward
            Assert.Equal("Max Power", state.GetField("Owner")?.Value);
        }

        /// <summary>
        /// A revision below the head is reported as such, which is what decides whether the
        /// restore button is offered.
        /// </summary>
        [Fact]
        public void GetStateAt_EarlierRevision_IsNotHead()
        {
            Seed(nameof(GetStateAt_EarlierRevision_IsNotHead));

            Write("low");
            Write("high");

            Assert.False(CoreHub.CommitManager.GetStateAt(ObjectId, 1).IsHead);
            Assert.True(CoreHub.CommitManager.GetStateAt(ObjectId, 2).IsHead);
        }

        /// <summary>
        /// The difference between two revisions is computed over their states, so a field that
        /// was changed and changed back does not show up as a difference.
        /// </summary>
        [Fact]
        public void DiffCommits_ComparesStates_NotIntermediateCommits()
        {
            Seed(nameof(DiffCommits_ComparesStates_NotIntermediateCommits));

            Write("low");
            Write("high");
            Write("low");

            var diff = CoreHub.CommitManager.DiffCommits(ObjectId, 1, 3);

            Assert.NotNull(diff);
            Assert.Empty(diff.Changes);
        }

        /// <summary>
        /// The difference reports the value at each end of the comparison.
        /// </summary>
        [Fact]
        public void DiffCommits_ReportsOldAndNewValue()
        {
            Seed(nameof(DiffCommits_ReportsOldAndNewValue));

            Write("low");
            Write("high");

            var diff = CoreHub.CommitManager.DiffCommits(ObjectId, 1, 2);

            var change = Assert.Single(diff.Changes);
            Assert.Equal("Severity", change.Name);
            Assert.Equal("low", change.OldValue);
            Assert.Equal("high", change.NewValue);
        }

        /// <summary>
        /// Restoring writes the historical values back and appends a commit rather than rewinding
        /// the chain.
        /// </summary>
        [Fact]
        public void RestoreCommit_WritesValuesBack_AndAppendsCommit()
        {
            Seed(nameof(RestoreCommit_WritesValuesBack_AndAppendsCommit));

            Write("low");
            Write("high");

            var result = CoreHub.CommitManager.RestoreCommit(ObjectId, 1, IdentityId);

            Assert.NotNull(result);
            Assert.True(result.Changed);
            Assert.Equal(CommitType.Restored, result.Commit.Type);
            Assert.Equal(3, result.Commit.Number);
            Assert.Equal("low", CoreHub.ValueManager.GetValue(ObjectId, SeverityFieldId)?.Data);
            Assert.Equal(3, CoreHub.CommitManager.GetHistory(ObjectId).Count());
        }

        /// <summary>
        /// Restoring a field that did not exist at the target revision clears it, so a restore
        /// reproduces the revision rather than merging into it.
        /// </summary>
        [Fact]
        public void RestoreCommit_ClearsFieldsAddedAfterTheRevision()
        {
            Seed(nameof(RestoreCommit_ClearsFieldsAddedAfterTheRevision));

            Write("low");
            CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = OwnerFieldId, Data = "Max Power" });

            CoreHub.CommitManager.RestoreCommit(ObjectId, 1, IdentityId);

            Assert.Null(CoreHub.ValueManager.GetValue(ObjectId, OwnerFieldId));
        }

        /// <summary>
        /// Restoring the head changes nothing and says so, rather than appending a commit that
        /// records no change.
        /// </summary>
        [Fact]
        public void RestoreCommit_OfHead_ChangesNothing()
        {
            Seed(nameof(RestoreCommit_OfHead_ChangesNothing));

            Write("low");

            var result = CoreHub.CommitManager.RestoreCommit(ObjectId, 1, IdentityId);

            Assert.NotNull(result);
            Assert.False(result.Changed);
            Assert.Single(CoreHub.CommitManager.GetHistory(ObjectId));
        }

        /// <summary>
        /// Creating an object opens its genesis commit, which records the system properties it
        /// was created with.
        /// </summary>
        [Fact]
        public void ObjectCreate_AppendsGenesisCommit()
        {
            Seed(nameof(ObjectCreate_AppendsGenesisCommit));

            var id = Guid.NewGuid();

            CoreHub.ObjectManager.Add(new ObjectEntity
            {
                Id = id,
                Key = "INC-00999",
                Summary = "Printer offline",
                WorkspaceId = WorkspaceId,
                ClassId = ClassId,
                CreatorId = IdentityId
            });

            var history = CoreHub.CommitManager.GetHistory(id).ToList();

            Assert.Single(history);
            Assert.Equal(CommitType.Created, history[0].Type);
            Assert.True(history[0].IsGenesis);
            Assert.Equal("Printer offline", history[0].GetChange("summary")?.NewValue);
            Assert.Equal("INC-00999", history[0].GetChange("key")?.NewValue);
        }

        /// <summary>
        /// Editing a system property of an object records what it was as well as what it became.
        /// </summary>
        [Fact]
        public void ObjectUpdate_RecordsSystemPropertyChange()
        {
            Seed(nameof(ObjectUpdate_RecordsSystemPropertyChange));

            var @object = CoreHub.ObjectManager.GetObject(ObjectId);
            @object.Summary = "VPN gateway unreachable";
            @object.UpdaterId = IdentityId;

            CoreHub.ObjectManager.Update(@object);

            var head = CoreHub.CommitManager.GetHead(ObjectId);

            Assert.NotNull(head);
            Assert.Equal(CommitType.Updated, head.Type);

            var change = head.GetChange("summary");
            Assert.Equal("VPN connection disrupted", change?.OldValue);
            Assert.Equal("VPN gateway unreachable", change?.NewValue);
        }

        /// <summary>
        /// An update that changed nothing writes no commit, so the history stays a record of
        /// actions rather than of saves.
        /// </summary>
        [Fact]
        public void ObjectUpdate_WithoutChange_AppendsNoCommit()
        {
            Seed(nameof(ObjectUpdate_WithoutChange_AppendsNoCommit));

            CoreHub.ObjectManager.Update(CoreHub.ObjectManager.GetObject(ObjectId));

            Assert.Empty(CoreHub.CommitManager.GetHistory(ObjectId));
        }

        /// <summary>
        /// Deleting an object appends a terminal commit and leaves the chain behind it standing —
        /// which is the part of the history an audit most often asks for.
        /// </summary>
        [Fact]
        public void ObjectRemove_AppendsTerminalCommit_AndKeepsHistory()
        {
            Seed(nameof(ObjectRemove_AppendsTerminalCommit_AndKeepsHistory));

            Write("high");

            CoreHub.ObjectManager.Remove(ObjectId);

            var history = CoreHub.CommitManager.GetHistory(ObjectId).ToList();

            Assert.Equal(2, history.Count);
            Assert.Equal(CommitType.Deleted, history[0].Type);
            Assert.Equal("INC-00123", history[0].ObjectKey);
            Assert.Null(CoreHub.ObjectManager.GetObject(ObjectId));
        }

        /// <summary>
        /// The commit carries the human-readable revision reference the concept specifies.
        /// </summary>
        [Fact]
        public void Commit_ExposesRevisionReference()
        {
            Seed(nameof(Commit_ExposesRevisionReference));

            Write("high");

            Assert.Equal("INC-00123#1", CoreHub.CommitManager.GetHead(ObjectId).Reference);
        }

        /// <summary>
        /// Appending a commit raises the event other components subscribe to.
        /// </summary>
        [Fact]
        public void CommitAdded_IsRaised()
        {
            Seed(nameof(CommitAdded_IsRaised));

            Commit observed = null;
            void Handler(object sender, Commit commit) => observed = commit;

            CoreHub.CommitManager.CommitAdded += Handler;

            try
            {
                Write("high");
            }
            finally
            {
                CoreHub.CommitManager.CommitAdded -= Handler;
            }

            Assert.NotNull(observed);
            Assert.Equal(ObjectId, observed.ObjectId);
        }

        /// <summary>
        /// Restoring raises the event that reports which revision was reapplied.
        /// </summary>
        [Fact]
        public void CommitRestored_IsRaised()
        {
            Seed(nameof(CommitRestored_IsRaised));

            Write("low");
            Write("high");

            CommitRestoreResult observed = null;
            void Handler(object sender, CommitRestoreResult result) => observed = result;

            CoreHub.CommitManager.CommitRestored += Handler;

            try
            {
                CoreHub.CommitManager.RestoreCommit(ObjectId, 1, IdentityId);
            }
            finally
            {
                CoreHub.CommitManager.CommitRestored -= Handler;
            }

            Assert.NotNull(observed);
            Assert.Equal(1, observed.RestoredNumber);
        }

        /// <summary>
        /// Computing a difference raises the event that reports it.
        /// </summary>
        [Fact]
        public void CommitDiffed_IsRaised()
        {
            Seed(nameof(CommitDiffed_IsRaised));

            Write("low");
            Write("high");

            CommitDiff observed = null;
            void Handler(object sender, CommitDiff diff) => observed = diff;

            CoreHub.CommitManager.CommitDiffed += Handler;

            try
            {
                CoreHub.CommitManager.DiffCommits(ObjectId, 1, 2);
            }
            finally
            {
                CoreHub.CommitManager.CommitDiffed -= Handler;
            }

            Assert.NotNull(observed);
            Assert.Equal(2, observed.To);
        }

        /// <summary>
        /// A class field that carries the name of a system property keeps its own entry in a
        /// replayed state: the two are different attributes and one must not overwrite the other.
        /// </summary>
        /// <remarks>
        /// The seeded classes model a <c>Description</c> field beside the object's own
        /// <c>description</c>, so this is the ordinary case rather than a contrived one.
        /// </remarks>
        [Fact]
        public void GetStateAt_FieldNamedLikeSystemProperty_KeepsBothEntries()
        {
            Seed(nameof(GetStateAt_FieldNamedLikeSystemProperty_KeepsBothEntries));

            var collidingFieldId = Guid.Parse("C07B8B96-E2AF-4E7F-D139-81B23D4E6077");

            using (var db = CoreHubFixture.CreateDbContext(nameof(GetStateAt_FieldNamedLikeSystemProperty_KeepsBothEntries)))
            {
                if (!db.Fields.Any(x => x.Id == collidingFieldId))
                {
                    db.Fields.Add(new Field { Id = collidingFieldId, Name = "Description", ClassId = ClassId });
                    db.SaveChanges();
                }
            }

            var @object = CoreHub.ObjectManager.GetObject(ObjectId);
            @object.Description = "the object's own description";
            @object.UpdaterId = IdentityId;

            using (CoreHub.CommitManager.BeginCommit(ObjectId, CommitType.Updated, IdentityId))
            {
                CoreHub.ObjectManager.Update(@object);
                CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = collidingFieldId, Data = "the field's description" });
            }

            var state = CoreHub.CommitManager.GetStateAt(ObjectId, 1);

            Assert.Equal(2, state.Fields.Count(x => string.Equals(x.Name, "description", StringComparison.OrdinalIgnoreCase)));
            Assert.Equal("the object's own description", state.GetByKey("system:description")?.Value);
            Assert.Equal("the field's description", state.GetByKey(collidingFieldId.ToString())?.Value);
        }

        /// <summary>
        /// Asking for a revision the chain does not have reports nothing rather than guessing.
        /// </summary>
        [Fact]
        public void GetStateAt_UnknownRevision_ReturnsNull()
        {
            Seed(nameof(GetStateAt_UnknownRevision_ReturnsNull));

            Write("high");

            Assert.Null(CoreHub.CommitManager.GetStateAt(ObjectId, 7));
            Assert.Null(CoreHub.CommitManager.GetCommit(ObjectId, 7));
            Assert.Null(CoreHub.CommitManager.DiffCommits(ObjectId, 1, 7));
        }

        /// <summary>
        /// Writes the severity field, producing one commit.
        /// </summary>
        /// <param name="data">The payload to write.</param>
        private static void Write(string data)
        {
            var existing = CoreHub.ValueManager.GetValue(ObjectId, SeverityFieldId);

            if (existing is null)
            {
                CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = SeverityFieldId, Data = data });

                return;
            }

            existing.Data = data;

            CoreHub.ValueManager.Update(existing);
        }
    }
}
