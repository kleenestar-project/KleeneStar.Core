using KleeneStar.Core.WebControl;
using KleeneStar.Model.Entities;
using System.Text.Json;

namespace KleeneStar.Core.Test.WebControl
{
    /// <summary>
    /// Tests <see cref="TemplateHierarchyNodeList"/>, the form the nodes of a hierarchy travel in.
    /// </summary>
    public class UnitTestTemplateHierarchyNodeList
    {
        /// <summary>
        /// The list arrives as JSON text (the wizard's hidden input), as a JSON string element or
        /// as the array itself (an API client); a blank value is an empty list, broken text no list.
        /// </summary>
        [Fact]
        public void TryParse_ReadsEveryForm()
        {
            var template = Guid.NewGuid();
            var text = $$"""[{"id":"new-1","template":"{{template}}","waitsFor":null}]""";

            Assert.True(TemplateHierarchyNodeList.TryParse(text, out var fromText));
            Assert.Equal(template, Assert.Single(fromText).Template);

            using var array = JsonDocument.Parse(text);
            Assert.True(TemplateHierarchyNodeList.TryParse(array.RootElement, out var fromArray));
            Assert.Equal(template, Assert.Single(fromArray).Template);

            using var quoted = JsonDocument.Parse(JsonSerializer.Serialize(text));
            Assert.True(TemplateHierarchyNodeList.TryParse(quoted.RootElement, out var fromString));
            Assert.Equal("new-1", Assert.Single(fromString).Id);

            Assert.True(TemplateHierarchyNodeList.TryParse("", out var empty));
            Assert.Empty(empty);
            Assert.False(TemplateHierarchyNodeList.TryParse("{not json", out _));
            Assert.False(TemplateHierarchyNodeList.TryParse("[null]", out _));
        }

        /// <summary>
        /// A row naming a stored node keeps its id, a new row gets a fresh one, and the parent
        /// and waits-for references follow the rows to their ids; a missing child flag counts as
        /// a sub-object and a link carries its direction.
        /// </summary>
        [Fact]
        public void ToNodes_KeepsStoredIdsAndTranslatesReferences()
        {
            var stored = new TemplateHierarchyNode { TemplateId = Guid.NewGuid() };
            var foreign = Guid.NewGuid();
            var rows = new List<TemplateHierarchyNodeList.Row>
            {
                new() { Id = stored.Id.ToString(), Template = stored.TemplateId },
                new() { Id = "new-1", Parent = stored.Id.ToString(), Template = Guid.NewGuid(), Class = Guid.Empty, Child = false, Link = "blocks:inverse" },
                new() { Id = foreign.ToString(), Parent = stored.Id.ToString(), Template = Guid.NewGuid(), WaitsFor = "new-1", Assignee = Guid.Empty, Link = "references" }
            };

            var nodes = TemplateHierarchyNodeList.ToNodes(rows, [stored]);

            Assert.Equal(stored.Id, nodes[0].Id);
            Assert.NotEqual(foreign, nodes[2].Id);
            Assert.Null(nodes[0].ParentNodeId);
            Assert.Equal(nodes[0].Id, nodes[1].ParentNodeId);
            Assert.Equal(nodes[0].Id, nodes[2].ParentNodeId);
            Assert.Equal(nodes[1].Id, nodes[2].PredecessorId);
            Assert.True(nodes[0].AsChild);
            Assert.False(nodes[1].AsChild);
            Assert.Equal("blocks", nodes[1].RelationTypeKey);
            Assert.True(nodes[1].RelationInverse);
            Assert.Equal("references", nodes[2].RelationTypeKey);
            Assert.False(nodes[2].RelationInverse);
            Assert.Null(nodes[1].ClassId);
            Assert.Null(nodes[2].AssigneeId);
        }

        /// <summary>
        /// Stored nodes written and read again yield the same tree.
        /// </summary>
        [Fact]
        public void Serialize_RoundTrips()
        {
            var first = new TemplateHierarchyNode { TemplateId = Guid.NewGuid(), ClassId = Guid.NewGuid(), AssigneeId = Guid.NewGuid() };
            var second = new TemplateHierarchyNode { TemplateId = Guid.NewGuid(), ParentNodeId = first.Id, AsChild = false, RelationTypeKey = "causes", RelationInverse = true };
            var third = new TemplateHierarchyNode { TemplateId = Guid.NewGuid(), PredecessorId = first.Id };

            Assert.True(TemplateHierarchyNodeList.TryParse(TemplateHierarchyNodeList.Serialize([first, second, third]), out var rows));

            var nodes = TemplateHierarchyNodeList.ToNodes(rows, [first, second, third]);
            Assert.Equal([first.Id, second.Id, third.Id], nodes.Select(x => x.Id));
            Assert.Equal(first.ClassId, nodes[0].ClassId);
            Assert.Equal(first.AssigneeId, nodes[0].AssigneeId);
            Assert.Equal(first.Id, nodes[1].ParentNodeId);
            Assert.False(nodes[1].AsChild);
            Assert.Equal("causes", nodes[1].RelationTypeKey);
            Assert.True(nodes[1].RelationInverse);
            Assert.Equal(first.Id, nodes[2].PredecessorId);
        }

        /// <summary>
        /// A link splits into key and direction and joins back.
        /// </summary>
        [Fact]
        public void Link_SplitsAndJoins()
        {
            Assert.Equal(("blocks", true), TemplateHierarchyNodeList.ParseLink(" blocks:inverse "));
            Assert.Equal(("blocks", false), TemplateHierarchyNodeList.ParseLink("blocks"));
            Assert.Equal(((string)null, false), TemplateHierarchyNodeList.ParseLink(""));
            Assert.Equal(((string)null, false), TemplateHierarchyNodeList.ParseLink(":inverse"));
            Assert.Equal("blocks:inverse", TemplateHierarchyNodeList.FormatLink("blocks", true));
            Assert.Null(TemplateHierarchyNodeList.FormatLink(null, true));
        }

        /// <summary>
        /// Rows waiting for each other in a circle are found; a chain is not a circle.
        /// </summary>
        [Fact]
        public void HasWaitingCycle_FindsCircles()
        {
            Assert.False(TemplateHierarchyNodeList.HasWaitingCycle(
            [
                new() { Id = "a" },
                new() { Id = "b", WaitsFor = "a" },
                new() { Id = "c", WaitsFor = "b" }
            ]));

            Assert.True(TemplateHierarchyNodeList.HasWaitingCycle(
            [
                new() { Id = "a", WaitsFor = "c" },
                new() { Id = "b", WaitsFor = "a" },
                new() { Id = "c", WaitsFor = "b" }
            ]));

            Assert.True(TemplateHierarchyNodeList.HasWaitingCycle([new() { Id = "a", WaitsFor = "a" }]));
        }
    }
}
