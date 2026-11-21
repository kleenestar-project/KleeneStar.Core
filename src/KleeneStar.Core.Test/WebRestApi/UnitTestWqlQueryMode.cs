using KleeneStar.Core.WebRestApi;
using System.Reflection;
using System.Runtime.CompilerServices;
using WebExpress.WebIndex.Queries;
using WebExpress.WebIndex.Wql;

namespace KleeneStar.Core.Test.WebRestApi
{
    /// <summary>
    /// Provides unit tests for the mode in which the REST endpoints turn WQL into a query.
    /// </summary>
    /// <remarks>
    /// The WebApp bases build their WQL query in <see cref="WqlQueryMode.Exact"/> unless an
    /// endpoint overrides <c>QueryMode</c>, and the exact tree holds the text matcher, which no
    /// database can run. The model's interceptor rewrites what slips through, but an endpoint
    /// backed by the database is meant to ask for <see cref="WqlQueryMode.Translatable"/>
    /// itself, and a new one is easily written without it.
    /// </remarks>
    public class UnitTestWqlQueryMode
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        /// Tests that every endpoint with its own query context - which in the core is always
        /// the database - builds its WQL query in the translatable mode.
        /// </summary>
        [Fact]
        public void EveryDatabaseEndpointQueriesTranslatable()
        {
            var endpoints = typeof(KleeneStarApplication).Assembly.GetTypes()
                .Where(x => x.IsClass && !x.IsAbstract && !x.ContainsGenericParameters)
                .Where(x => x.GetProperty("QueryMode", Instance)?.PropertyType == typeof(WqlQueryMode))
                .Where(x => x.GetMethod("CreateContext", Instance, Type.EmptyTypes) is { } method
                    && method.ReturnType == typeof(IQueryContext)
                    && method.DeclaringType.Assembly == typeof(KleeneStarApplication).Assembly)
                .ToArray();

            Assert.NotEmpty(endpoints);
            Assert.All(endpoints, x => Assert.True(Mode(x) == WqlQueryMode.Translatable,
                $"{x.FullName} queries the database and must return WqlQueryMode.Translatable from QueryMode."));
        }

        /// <summary>
        /// Tests that every table of the application inherits the translatable mode.
        /// </summary>
        [Fact]
        public void EveryTableQueriesTranslatable()
        {
            var tables = typeof(KleeneStarApplication).Assembly.GetTypes()
                .Where(x => x.IsClass && !x.IsAbstract && !x.ContainsGenericParameters)
                .Where(x => IsTable(x))
                .ToArray();

            Assert.NotEmpty(tables);
            Assert.All(tables, x => Assert.Equal(WqlQueryMode.Translatable, Mode(x)));
        }

        /// <summary>
        /// Reads the query mode of an endpoint without running its constructor, which would
        /// need the hubs; the property is a constant.
        /// </summary>
        /// <param name="type">The endpoint type.</param>
        /// <returns>The query mode.</returns>
        private static WqlQueryMode Mode(Type type)
        {
            var endpoint = RuntimeHelpers.GetUninitializedObject(type);

            return (WqlQueryMode)type.GetProperty("QueryMode", Instance).GetValue(endpoint);
        }

        /// <summary>
        /// Determines whether a type derives from the application's table base.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>True for a table.</returns>
        private static bool IsTable(Type type)
        {
            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(KleeneStarRestApiTable<>))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
