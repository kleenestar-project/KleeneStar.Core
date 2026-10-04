using KleeneStar.Core.WebManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebParameter;
using WebExpress.WebCore.WebUri;

namespace KleeneStar.Core.WebUri
{
    /// <summary>
    /// Variable path segment.
    /// </summary>
    /// <typeparam name="TParameter">The parameter type.</typeparam>
    public class InsightIdUriPathSegmentVariable<TParameter> : UriPathSegmentVariableGuid<TParameter>
        where TParameter : IParameterStatic, new()
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="tag">The tag or null</param>
        public InsightIdUriPathSegmentVariable(object tag = null)
            : base(tag)
        {
        }

        /// <summary>
        /// Returns the variable.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The variable value pair.</returns>
        public override IDictionary<string, string> GetVariable(string value)
        {
            return new Dictionary<string, string>();
        }

        /// <summary>
        /// Make a deep copy.
        /// </summary>
        /// <returns>The copy.</returns>
        public override IUriPathSegment Copy()
        {
            return new InsightIdUriPathSegmentVariable<TParameter>(this)
            {
                Expression = Expression,
                Value = Value,
                IsHidden = IsHidden,
                Uri = Uri
            };
        }

        /// <summary>
        /// Checks whether the node matches the path element.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>True if the path element matched, false otherwise.</returns>
        public override bool IsMatched(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            else if (InsightManager.ReservedInsightNames.Contains(value?.Trim().ToLower()))
            {
                return false;
            }
            else if (string.IsNullOrWhiteSpace(Expression) && Value.Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else if (Regex.IsMatch(value, Expression, RegexOptions.IgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns a string that represents the display text for the current instance.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>
        /// A string containing the display text associated with the instance. The 
        /// value may be empty if no display text is available.
        /// </returns>
        public override string GetDisplayText(IRenderContext renderContext)
        {
            var guid = Guid.TryParse(Value, out var id) ? id : Guid.Empty;
            var insight = CoreHub.InsightManager.GetInsight(guid);

            return insight?.Name;
        }

        /// <summary>
        /// Returns an icon that visually represents the parameter within the given render context.
        /// </summary>
        /// <param name="renderContext">
        /// The rendering context that provides information required to determine the appropriate icon.
        /// </param>
        /// <returns>
        /// An icon associated with the current instance. The value may be <c>null</c> or empty 
        /// if no icon is available.
        /// </returns>
        public override IIcon GetIcon(IRenderContext renderContext)
        {
            var guid = Guid.TryParse(Value, out var id) ? id : Guid.Empty;
            var insight = CoreHub.InsightManager.GetInsight(guid);

            return insight?.Icon;
        }
    }
}
