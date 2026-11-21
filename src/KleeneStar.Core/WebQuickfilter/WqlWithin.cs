using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebIndex;
using WebExpress.WebCore;
using WebExpress.WebIndex.Wql;
using WebExpress.WebIndex.Wql.Predicate;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebQuickfilter
{
    /// <summary>
    /// The WQL predicate <c>within("KEY")</c>: true for every object below the object of that key,
    /// at any depth; <c>not within("KEY")</c> is its negation (<c>kleenestar.hierarchy.md</c>).
    /// </summary>
    /// <remarks>
    /// The predicate is a <see cref="WqlIdPredicate{TIndexItem}"/> over
    /// <see cref="ObjectEntity"/>: the parser binds the call to the subtree ids
    /// (<see cref="WebManager.ObjectHierarchy.GetDescendantIds"/>, one range query on the path
    /// index), and the condition is the <c>Contains</c> of that set. The object of the key is
    /// read through <see cref="WebManager.IObjectManager.GetObjectByKey"/>, so an unknown or
    /// unreadable key binds the empty set and selects nothing. A call whose argument is no
    /// string is a parse error.
    /// <para>
    /// <see cref="Register"/> registers the predicate with the index manager of the host
    /// (<see cref="IndexManager.RegisterWqlPredicate"/>), so every WebApp REST endpoint over
    /// objects and the WQL prompt know it; <see cref="CreateParser{TIndexItem}"/> answers a
    /// parser that knows it with or without a host.
    /// </para>
    /// </remarks>
    public static class WqlWithin
    {
        /// <summary>
        /// The name of the predicate.
        /// </summary>
        public const string Name = "within";

        /// <summary>
        /// Gets the predicate with the default resolution of a key.
        /// </summary>
        public static IWqlPredicate<ObjectEntity> Predicate { get; } = Create();

        /// <summary>
        /// Creates the predicate.
        /// </summary>
        /// <param name="resolve">
        /// Answers the subtree ids of a key, <see langword="null"/> for an unknown key; the
        /// default reads the object narrowed and its descendants.
        /// </param>
        /// <returns>The predicate.</returns>
        public static IWqlPredicate<ObjectEntity> Create(Func<string, IEnumerable<Guid>> resolve = null)
        {
            resolve ??= Subtree;

            return new WqlIdPredicate<ObjectEntity>(Name, arguments =>
            {
                if (arguments is null || arguments.Count != 1 || arguments[0] is not string key)
                {
                    throw new ArgumentException("within expects one object key as a string.");
                }

                return resolve(key.Trim()) ?? [];
            });
        }

        /// <summary>
        /// Registers the predicate with the index manager of the host. Without a host nothing
        /// is registered.
        /// </summary>
        public static void Register()
        {
            WebEx.ComponentHub?.GetComponentManager<IndexManager>()?.RegisterWqlPredicate(Predicate);
        }

        /// <summary>
        /// Creates the parser KleeneStar reads stored and submitted WQL with: the parser of the
        /// index manager (registered functions and predicates), else a plain parser; a parser
        /// over objects knows <c>within</c> in either case.
        /// </summary>
        /// <typeparam name="TIndexItem">The type the expression is written against.</typeparam>
        /// <returns>The parser.</returns>
        public static WqlParser<TIndexItem> CreateParser<TIndexItem>()
            where TIndexItem : WebExpress.WebIndex.IIndexItem
        {
            var parser = WebEx.ComponentHub?.GetComponentManager<IndexManager>()?.CreateWqlParser<TIndexItem>()
                ?? new WqlParser<TIndexItem>();

            if (parser is WqlParser<ObjectEntity> objects
                && !objects.PredicateNames.Contains(Name, StringComparer.OrdinalIgnoreCase))
            {
                objects.RegisterPredicate(Predicate);
            }

            return parser;
        }

        /// <summary>
        /// Answers the ids below the object of a key, read narrowed.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The ids, or <see langword="null"/> for an unknown or unreadable key.</returns>
        private static IEnumerable<Guid> Subtree(string key)
        {
            var root = string.IsNullOrWhiteSpace(key) ? null : CoreHub.ObjectManager.GetObjectByKey(key);

            return root is null ? null : WebManager.ObjectHierarchy.GetDescendantIds(root.Id).ToList();
        }
    }
}
