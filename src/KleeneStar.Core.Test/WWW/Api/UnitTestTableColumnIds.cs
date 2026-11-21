using System.Reflection;
using System.Runtime.CompilerServices;
using WebExpress.WebApp.WebRestApi;

namespace KleeneStar.Core.Test.WWW.Api
{
    /// <summary>
    /// Every table endpoint names each of its columns once. The framework keys the columns by id
    /// when it lays a stored layout over them (<c>RestApiTableLayout.Apply</c>), so a duplicate id -
    /// the tenant table once declared its workspace column as a second <c>description</c> - makes
    /// every request of that table fail with "An item with the same key has already been added".
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestTableColumnIds
    {
        /// <summary>
        /// The default columns of every table endpoint of the core carry distinct ids.
        /// </summary>
        [Fact]
        public void DefaultColumns_HaveDistinctIds()
        {
            CoreHubFixture.Initialize(nameof(DefaultColumns_HaveDistinctIds));

            var tables = typeof(CoreHub).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters && IsTable(t))
                .ToList();
            var checkedTables = 0;
            var duplicates = new List<string>();

            foreach (var type in tables)
            {
                // the constructors resolve row option routes through the sitemap, which the test
                // host does not wire; the columns need none of that
                var endpoint = RuntimeHelpers.GetUninitializedObject(type);
                var retrieve = type.GetMethod("RetrieveDefaultColumns", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (retrieve is null)
                {
                    continue;
                }

                List<RestApiTableColumn> columns;

                try
                {
                    columns = [.. ((IEnumerable<RestApiTableColumn>)retrieve.Invoke(endpoint, [null])) ?? []];
                }
                catch (Exception ex) when (ex is TargetInvocationException or NullReferenceException or InvalidOperationException)
                {
                    // a table whose columns need the route or the sitemap (a class, a saved
                    // search, a link address) cannot be asked here - the columns are iterators,
                    // so that shows while they are read, not when the method is called
                    continue;
                }

                checkedTables++;

                duplicates.AddRange(columns
                    .GroupBy(c => c.Id, StringComparer.Ordinal)
                    .Where(g => g.Count() > 1)
                    .Select(g => $"{type.FullName}: {g.Key}"));
            }

            Assert.True(checkedTables >= 10, $"only {checkedTables} of {tables.Count} tables could be asked");
            Assert.Empty(duplicates);
        }

        private static bool IsTable(Type type)
        {
            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(RestApiTable<>))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
