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
        /// and the link targets follow the rows to their ids; a missing child flag counts as a
        /// sub-object, a link carries its direction, and a target naming no row, the row itself
        /// or nothing is dropped with the links left empty.
        /// </summary>
        [Fact]
        public void ToNodes_KeepsStoredIdsAndTranslatesReferences()
        {
            var stored = new TemplateHierarchyNode { TemplateId = Guid.NewGuid() };
            var foreign = Guid.NewGuid();
            var rows = new List<TemplateHierarchyNodeList.Row>
            {
                new() { Id = stored.Id.ToString(), Template = stored.TemplateId },
                new()
                {
                    Id = "new-1", Parent = stored.Id.ToString(), Template = Guid.NewGuid(), Class = Guid.Empty, Child = false,
                    Links = [new() { Link = "blocks:inverse", Targets = ["ROOT", "new-1", "unknown"] }, new() { Link = "", Targets = ["root"] }]
                },
                new()
                {
                    Id = foreign.ToString(), Parent = stored.Id.ToString(), Template = Guid.NewGuid(), Assignee = Guid.Empty,
                    Links = [new() { Link = "references", Targets = ["new-1", stored.Id.ToString()] }, new() { Link = "causes", Targets = ["unknown"] }]
                }
            };

            var nodes = TemplateHierarchyNodeList.ToNodes(rows, [stored]);

            Assert.Equal(stored.Id, nodes[0].Id);
            Assert.NotEqual(foreign, nodes[2].Id);
            Assert.Null(nodes[0].ParentNodeId);
            Assert.Equal(nodes[0].Id, nodes[1].ParentNodeId);
            Assert.Equal(nodes[0].Id, nodes[2].ParentNodeId);
            Assert.True(nodes[0].AsChild);
            Assert.False(nodes[1].AsChild);
            Assert.Empty(nodes[0].Links);

            var blocked = Assert.Single(nodes[1].Links);
            Assert.Equal("blocks", blocked.Type);
            Assert.True(blocked.Inverse);
            Assert.Equal([TemplateHierarchyNodeLink.RootTarget], blocked.Targets);

            var references = Assert.Single(nodes[2].Links);
            Assert.Equal("references", references.Type);
            Assert.False(references.Inverse);
            Assert.Equal([nodes[1].Id.ToString(), nodes[0].Id.ToString()], references.Targets);

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
            var second = new TemplateHierarchyNode { TemplateId = Guid.NewGuid(), ParentNodeId = first.Id, AsChild = false };
            var third = new TemplateHierarchyNode { TemplateId = Guid.NewGuid() };
            second.Links = [new() { Type = "causes", Inverse = true, Targets = [TemplateHierarchyNodeLink.RootTarget, third.Id.ToString()] }];
            third.Links = [new() { Type = "blocks", Inverse = true, Targets = [first.Id.ToString()] }];

            Assert.True(TemplateHierarchyNodeList.TryParse(TemplateHierarchyNodeList.Serialize([first, second, third]), out var rows));

            var nodes = TemplateHierarchyNodeList.ToNodes(rows, [first, second, third]);
            Assert.Equal([first.Id, second.Id, third.Id], nodes.Select(x => x.Id));
            Assert.Equal(first.ClassId, nodes[0].ClassId);
            Assert.Equal(first.AssigneeId, nodes[0].AssigneeId);
            Assert.Equal(first.Id, nodes[1].ParentNodeId);
            Assert.False(nodes[1].AsChild);

            var causes = Assert.Single(nodes[1].Links);
            Assert.Equal("causes", causes.Type);
            Assert.True(causes.Inverse);
            Assert.Equal([TemplateHierarchyNodeLink.RootTarget, third.Id.ToString()], causes.Targets);

            var blocks = Assert.Single(nodes[2].Links);
            Assert.Equal("blocks", blocks.Type);
            Assert.Equal([first.Id.ToString()], blocks.Targets);
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
        /// Blocking links running in a circle are found, in either direction and through the
        /// root; a chain is not a circle, and links of a type that does not block never are.
        /// </summary>
        [Fact]
        public void HasBlockingCycle_FindsCircles()
        {
            static bool Blocking(string key) => key == "blocks";
            static TemplateHierarchyNodeList.LinkRow Link(string link, params string[] targets) => new() { Link = link, Targets = [.. targets] };

            // b is blocked by a, c by b: a chain
            Assert.False(TemplateHierarchyNodeList.HasBlockingCycle(
            [
                new() { Id = "a" },
                new() { Id = "b", Links = [Link("blocks:inverse", "a")] },
                new() { Id = "c", Links = [Link("blocks:inverse", "b")] }
            ], Blocking));

            // a blocks b, b blocks c, c is blocking a
            Assert.True(TemplateHierarchyNodeList.HasBlockingCycle(
            [
                new() { Id = "a", Links = [Link("blocks", "b")] },
                new() { Id = "b", Links = [Link("blocks", "c")] },
                new() { Id = "c", Links = [Link("blocks", "a")] }
            ], Blocking));

            // the root blocks a, and a blocks the root
            Assert.True(TemplateHierarchyNodeList.HasBlockingCycle(
            [
                new() { Id = "a", Links = [Link("blocks:inverse", "root"), Link("blocks", "root")] }
            ], Blocking));

            // the same circle with a relation that does not block
            Assert.False(TemplateHierarchyNodeList.HasBlockingCycle(
            [
                new() { Id = "a", Links = [Link("references", "b")] },
                new() { Id = "b", Links = [Link("references", "a")] }
            ], Blocking));
        }
    }
}
