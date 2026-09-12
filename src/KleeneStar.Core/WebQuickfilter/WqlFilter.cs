using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using WebExpress.WebIndex;
using WebExpress.WebIndex.Wql;

namespace KleeneStar.Core.WebQuickfilter
{
    /// <summary>
    /// Turns a stored WQL expression into a condition a query or a sequence can be narrowed by.
    /// </summary>
    /// <remarks>
    /// The statement's own <c>ToQuery</c> starts from an empty query and would drop everything
    /// applied so far, so only its filter condition is taken and left to the caller to add to
    /// whatever it is already narrowing. The same compiled condition serves both a query (where
    /// the store evaluates it) and a materialized sequence (where views that compose their rows
    /// from several sources evaluate it in memory), so the two never disagree about what an
    /// expression means.
    /// </remarks>
    public static class WqlFilter
    {
        /// <summary>
        /// Compiles a WQL expression into a condition.
        /// </summary>
        /// <typeparam name="TIndexItem">The type the expression is written against.</typeparam>
        /// <param name="wql">The expression to compile.</param>
        /// <returns>
        /// The condition, or <see langword="null"/> when the expression is empty, carries no
        /// filter, or no longer parses - a stored expression that went bad must not take the
        /// whole view down.
        /// </returns>
        public static Expression<Func<TIndexItem, bool>> Compile<TIndexItem>(string wql)
            where TIndexItem : IIndexItem
        {
            if (string.IsNullOrWhiteSpace(wql))
            {
                return null;
            }

            try
            {
                var statement = new WqlParser<TIndexItem>().Parse(wql);

                if (statement is null || statement.HasErrors || statement.Filter is null)
                {
                    return null;
                }

                var param = Expression.Parameter(typeof(TIndexItem), "x");
                var body = statement.Filter.ToExpression(param);

                return Expression.Lambda<Func<TIndexItem, bool>>(body, param);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Checks whether a WQL expression can be evaluated against a type, so a surface that
        /// stores one can refuse a broken expression at the moment it is written rather than
        /// silently ignoring it on every read.
        /// </summary>
        /// <typeparam name="TIndexItem">The type the expression is written against.</typeparam>
        /// <param name="wql">The expression to check; blank is valid and means no filter.</param>
        /// <param name="error">The reason when it is not.</param>
        /// <returns><see langword="true"/> when the expression is empty or compiles.</returns>
        public static bool TryValidate<TIndexItem>(string wql, out string error)
            where TIndexItem : IIndexItem
        {
            error = null;

            if (string.IsNullOrWhiteSpace(wql))
            {
                return true;
            }

            try
            {
                var statement = new WqlParser<TIndexItem>().Parse(wql);

                if (statement is null)
                {
                    error = "The filter could not be parsed.";

                    return false;
                }

                if (statement.HasErrors)
                {
                    error = statement.Error?.ToString();
                    error = string.IsNullOrWhiteSpace(error) ? "The filter could not be parsed." : error;

                    return false;
                }

                if (statement.Filter is null)
                {
                    error = "The filter carries no condition.";

                    return false;
                }

                // an expression that parses can still name what the type does not have; the
                // translation is where that surfaces
                var param = Expression.Parameter(typeof(TIndexItem), "x");
                var body = statement.Filter.ToExpression(param);

                Expression.Lambda<Func<TIndexItem, bool>>(body, param).Compile();

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;

                return false;
            }
        }

        /// <summary>
        /// Narrows a materialized sequence by a WQL expression.
        /// </summary>
        /// <typeparam name="TIndexItem">The type of the items.</typeparam>
        /// <param name="wql">The expression, or blank for no narrowing.</param>
        /// <param name="items">The sequence to narrow.</param>
        /// <returns>The narrowed sequence; the sequence itself when the expression yields no condition.</returns>
        public static IEnumerable<TIndexItem> Apply<TIndexItem>(string wql, IEnumerable<TIndexItem> items)
            where TIndexItem : IIndexItem
        {
            var predicate = Compile<TIndexItem>(wql);

            if (predicate is null)
            {
                return items;
            }

            var compiled = predicate.Compile();

            // an item the condition cannot be asked of - a text operator on an attribute that
            // is null, say - does not match; it must not take the whole sequence down
            return items.Where(x =>
            {
                try
                {
                    return compiled(x);
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}
