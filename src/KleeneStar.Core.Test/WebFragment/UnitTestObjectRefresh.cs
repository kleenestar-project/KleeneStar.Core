using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebInclude;
using System.Reflection;
using System.Text;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Core.Test.WebFragment
{
    /// <summary>
    /// Tests the two halves of the reload after a dialog changed an object: the page names
    /// its object in a meta tag, and a script scoped to the same pages reads that tag. The
    /// halves only meet by name, so the name is what these tests hold together.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectRefresh
    {
        /// <summary>
        /// The four detail pages the object dialogs open on.
        /// </summary>
        private static readonly Type[] DetailPages =
        [
            typeof(global::KleeneStar.Core.WWW.Issue._objectkey_.Index),
            typeof(global::KleeneStar.Core.WWW.Asset._objectkey_.Index),
            typeof(global::KleeneStar.Core.WWW.Document._objectkey_.Index),
            typeof(global::KleeneStar.Core.WWW.Blog._objectkey_.Index),

            // the detail page of a kind an add-on contributes
            typeof(global::KleeneStar.Core.WebScope.IScopeObjectKindDetail)
        ];

        /// <summary>
        /// Reads the scope types a type declares.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The scope types.</returns>
        private static List<Type> Scopes(Type type)
        {
            return type.GetCustomAttributes()
                .Select(x => x.GetType())
                .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ScopeAttribute<>))
                .Select(x => x.GetGenericArguments()[0])
                .ToList();
        }

        /// <summary>
        /// Reads the embedded script the include names.
        /// </summary>
        /// <returns>The script.</returns>
        private static string Script()
        {
            var assembly = typeof(IncludeObjectRefreshScript).Assembly;
            var name = assembly.GetManifestResourceNames()
                .FirstOrDefault(x => x.Replace('\\', '/').EndsWith("js/kleenestar.core.object.refresh.js", StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(name);

            using var stream = assembly.GetManifestResourceStream(name!);
            using var reader = new StreamReader(stream!, Encoding.UTF8);

            return reader.ReadToEnd();
        }

        /// <summary>
        /// The include is a discoverable plugin include, names the embedded script, and is not
        /// scoped: a preview pane is embedded into the overviews, the boards and the insights
        /// alike. The script follows only pages and previews that name an object, so a list page
        /// is not reloaded because a row was edited in a dialog; its table refreshes on its own.
        /// </summary>
        [Fact]
        public void Include_IsUnscoped()
        {
            var type = typeof(IncludeObjectRefreshScript);
            var asset = type.GetCustomAttribute<AssetAttribute>();

            Assert.True(type.IsSealed && type.IsPublic);
            Assert.True(typeof(IInclude).IsAssignableFrom(type));
            Assert.NotNull(asset);
            Assert.Empty(Scopes(type));

            // on the overview the home page's own fragment names the object the script reloads for
            Assert.Equal
            (
                [typeof(global::KleeneStar.Core.WWW.Documents._workspacekey_.Index)],
                Scopes(typeof(global::KleeneStar.Core.WebFragment.Object.Documents.DocumentHomeRefreshFragment))
            );
        }

        /// <summary>
        /// The fragment names the object on the detail pages and on the preview a master-detail
        /// pane embeds, and the tags it writes are the tags the script reads.
        /// </summary>
        [Fact]
        public void Fragment_AndScript_AgreeOnTheMetaTags()
        {
            Assert.Equal
            (
                DetailPages.Append(typeof(global::KleeneStar.Core.WWW.Issue._objectkey_.Preview)).OrderBy(x => x.FullName),
                Scopes(typeof(ObjectRefreshFragment)).OrderBy(x => x.FullName)
            );

            var script = Script();

            Assert.Contains($"\"{ObjectRefreshFragment.MetaName}\"", script);
            Assert.Contains($"\"{ObjectRefreshFragment.DomainMetaName}\"", script);
        }

        /// <summary>
        /// The script keeps out of the way of forms that are not about the page's object: it
        /// reads the success event, matches the form's id against the page's, and leaves a
        /// delete alone.
        /// </summary>
        [Fact]
        public void Script_ReloadsOnlyForTheEditedObject()
        {
            var script = Script();

            Assert.Contains("UPLOAD_SUCCESS_EVENT", script);
            Assert.Contains("location.reload()", script);
            Assert.Contains("\"delete\"", script);
            Assert.Contains("formId !== id", script);
        }

        /// <summary>
        /// A workflow transition is run in the background and laid over the page instead of
        /// reloading it: the script reads the endpoint from the tag the fragment writes and
        /// recognises the transition dialog by the input its data service reads the state from.
        /// </summary>
        [Fact]
        public void Script_RunsTransitionsWithoutReload()
        {
            var script = Script();

            Assert.Contains($"\"{ObjectRefreshFragment.TransitionMetaName}\"", script);
            Assert.Contains($"\"{(global::KleeneStar.Core.WWW.Api._1_.Objects.Transition.StateKey)}\"", script);
            Assert.Contains("preventDefault()", script);
            Assert.Contains("DOMParser", script);
        }

        /// <summary>
        /// A preview pane is followed through the markup its frame loads, and a change announced
        /// on the data change channel brings the views of the object up to date with a flash.
        /// </summary>
        [Fact]
        public void Script_FollowsPreviewsAndChanges()
        {
            var script = Script();

            Assert.Contains("DATA_ARRIVED_EVENT", script);
            Assert.Contains("FrameCtrl", script);
            Assert.Contains("\"webexpress.webapp.data.changed\"", script);
            Assert.Contains("subscribeDomains", script);
            Assert.Contains("\"ks-change-flash\"", script);
        }
    }
}
