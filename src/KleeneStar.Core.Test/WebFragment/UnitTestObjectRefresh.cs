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
            typeof(global::KleeneStar.Core.WWW.Blog._objectkey_.Index)
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
                .FirstOrDefault(x => x.Replace('\\', '/').EndsWith("js/objectrefresh.js", StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(name);

            using var stream = assembly.GetManifestResourceStream(name!);
            using var reader = new StreamReader(stream!, Encoding.UTF8);

            return reader.ReadToEnd();
        }

        /// <summary>
        /// The include is a discoverable plugin include, names the embedded script, and is
        /// scoped to exactly the detail pages - a list page must not reload because a row was
        /// edited in a dialog; its table refreshes on its own.
        /// </summary>
        [Fact]
        public void Include_IsScopedToTheDetailPages()
        {
            var type = typeof(IncludeObjectRefreshScript);
            var asset = type.GetCustomAttribute<AssetAttribute>();

            Assert.True(type.IsSealed && type.IsPublic);
            Assert.True(typeof(IInclude).IsAssignableFrom(type));
            Assert.NotNull(asset);
            Assert.Equal(DetailPages.OrderBy(x => x.FullName), Scopes(type).OrderBy(x => x.FullName));
        }

        /// <summary>
        /// The fragment stands on the same pages as the script, and the tag it writes is the
        /// tag the script reads.
        /// </summary>
        [Fact]
        public void Fragment_AndScript_AgreeOnTheMetaTag()
        {
            Assert.Equal(DetailPages.OrderBy(x => x.FullName), Scopes(typeof(ObjectRefreshFragment)).OrderBy(x => x.FullName));
            Assert.Contains($"\"{ObjectRefreshFragment.MetaName}\"", Script());
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
    }
}
