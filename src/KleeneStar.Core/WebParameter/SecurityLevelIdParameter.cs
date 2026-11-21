using System;
using WebExpress.WebCore.WebParameter;

namespace KleeneStar.Core.WebParameter
{
    /// <summary>
    /// Represents a parameter that specifies a security level id.
    /// </summary>
    public sealed class SecurityLevelIdParameter : IParameterStatic
    {
        /// <summary>
        /// Gets the key that uniquely identifies the parameter in configuration or
        /// settings contexts.
        /// </summary>
        public static string Key => "securitylevelid";

        /// <summary>
        /// Gets or sets the scope of the parameter.
        /// </summary>
        public ParameterScope Scope { get; set; }

        /// <summary>
        /// Gets or sets the value of the parameter.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public SecurityLevelIdParameter()
        {
        }

        /// <summary>
        /// Initializes a new instance of the class with a specified value.
        /// </summary>
        /// <param name="value">The value of the parameter.</param>
        public SecurityLevelIdParameter(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Initializes a new instance of the class with a specified value.
        /// </summary>
        /// <param name="value">The value of the parameter.</param>
        public SecurityLevelIdParameter(Guid value)
        {
            Value = value.ToString();
        }

        /// <summary>
        /// Retrieves the unique key associated with the current instance.
        /// </summary>
        /// <returns>
        /// A string representing the unique key. This key is used for identifying 
        /// the instance in various operations.
        /// </returns>
        public string GetKey()
        {
            return Key;
        }
    }
}
