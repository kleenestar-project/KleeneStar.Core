using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Model.Entities;
using System.Reflection;
using System.Runtime.CompilerServices;
using WebExpress.WebApp.WebRestApi;

namespace KleeneStar.Core.Test.WWW.Api.Classes
{
    /// <summary>
    /// Tests the object type column of the class table: it is offered as a chip column, it
    /// shows the label of the registered kind, and it degrades to the stored key for a kind
    /// nothing registered - the key of an uninstalled add-on is still worth reading.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestClassTableKindColumn
    {
        /// <summary>
        /// The column is declared, visible by default, and drawn through the tag template -
        /// which is what renders a cell as chips on the client.
        /// </summary>
        [Fact]
        public void DefaultColumns_CarryTheKindAsChips()
        {
            CoreHubFixture.Initialize(nameof(DefaultColumns_CarryTheKindAsChips));

            // the constructor resolves the row option routes through the sitemap, which the
            // test host does not wire; the columns need none of that, so the instance is
            // created around the constructor
            var endpoint = (global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.Table)RuntimeHelpers.GetUninitializedObject(typeof(global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.Table));
            var retrieve = endpoint.GetType().GetMethod("RetrieveDefaultColumns", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(retrieve);

            var columns = Assert.IsAssignableFrom<IEnumerable<RestApiTableColumn>>(retrieve!.Invoke(endpoint, [null])).ToList();
            var kind = columns.SingleOrDefault(x => x.Id == "kind");

            Assert.NotNull(kind);
            Assert.True(kind!.Visible);
            Assert.IsType<RestApiTableColumnTemplateTag>(kind.Template);

            // the cells are positional, so the column has to sit where the row writes it:
            // after the description and before the state
            Assert.Equal(columns.FindIndex(x => x.Id == "description") + 1, columns.FindIndex(x => x.Id == "kind"));
            Assert.Equal(columns.FindIndex(x => x.Id == "kind") + 1, columns.FindIndex(x => x.Id == "state"));
        }

        /// <summary>
        /// A registered kind is shown by its label, an unregistered one by its key, and a
        /// class that names no kind by the label of the default kind - the same reading the
        /// object overviews give the stored key.
        /// </summary>
        [Fact]
        public void ResolveKindLabel_ReadsTheCatalog()
        {
            CoreHubFixture.Initialize(nameof(ResolveKindLabel_ReadsTheCatalog));

            var document = ObjectKindCatalog.GetKind(ObjectKind.Document);

            Assert.NotNull(document);

            var registered = global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.Table.ResolveKindLabel(new Model.Entities.Class { Kind = "Document" }, null);
            var unset = global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.Table.ResolveKindLabel(new Model.Entities.Class { Kind = null }, null);
            var unknown = global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.Table.ResolveKindLabel(new Model.Entities.Class { Kind = "Wiki-Space" }, null);

            // a translation may or may not be loaded in the test host; either way the chip is
            // not the raw key of a registered kind
            Assert.False(string.IsNullOrWhiteSpace(registered));
            Assert.NotEqual("document", registered);
            Assert.False(string.IsNullOrWhiteSpace(unset));
            Assert.Equal("wiki-space", unknown);
        }
    }
}
