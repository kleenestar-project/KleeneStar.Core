using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using System.Collections.Generic;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebParameter;
using WebExpress.WebCore.WebUri;

namespace KleeneStar.Core.WebUri
{
    /// <summary>
    /// The variable path segment of an object key.
    /// </summary>
    /// <remarks>
    /// The segment is an <see cref="IUriPathSegmentTrail"/>: the breadcrumb shows the ancestors
    /// of the object (<see cref="ObjectHierarchy.GetVisibleAncestry"/>) before the object itself,
    /// each as a crumb with the key, the class icon and the detail page.
    /// </remarks>
    /// <typeparam name="TParameter">The parameter type.</typeparam>
    public class ObjectKeyUriPathSegmentVariable<TParameter> : UriPathSegmentVariableRegex<TParameter>, IUriPathSegmentTrail
        where TParameter : IParameterStatic, new()
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="tag">The tag or null</param>
        public ObjectKeyUriPathSegmentVariable(object tag = null)
            : base(@"^[a-z0-9-]{1,10}-\d+$", tag)
        {
        }

        /// <summary>
        /// Make a deep copy.
        /// </summary>
        /// <returns>The copy.</returns>
        public override IUriPathSegment Copy()
        {
            return new ObjectKeyUriPathSegmentVariable<TParameter>()
            {
                Expression = Expression,
                Value = Value,
                IsHidden = IsHidden,
                Uri = Uri
            };
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
            return Value;
        }

        /// <summary>
        /// Returns an icon that visually represents the parameter within the given render context.
        /// </summary>
        /// <param name="renderContext">
        /// The rendering context that provides information required to determine the appropriate icon.
        /// </param>
        /// <returns>
        /// The icon of the object's class, or the object's own when the class carries none; 
        /// <c>null</c> when the key names no object.
        /// </returns>
        /// <remarks>
        /// Resolved through <see cref="ObjectIcon"/> like every other surface: the picture an
        /// object carries itself is a copy written once at creation and never updated, so the
        /// breadcrumb read off it kept showing the class avatar of that day while the lists
        /// beside it showed the current one.
        /// </remarks>
        public override IIcon GetIcon(IRenderContext renderContext)
        {
            var @object = CoreHub.ObjectManager.GetObjectByKey(Value);

            return ObjectIcon.Resolve(@object);
        }

        /// <summary>
        /// Returns the crumbs of the object and its ancestors, from the root to the object.
        /// </summary>
        /// <param name="renderContext">The context of the page the breadcrumb is rendered on.</param>
        /// <returns>The crumbs; a single crumb of the key when the key names no readable object.</returns>
        public IEnumerable<UriTrailCrumb> GetTrail(IRenderContext renderContext)
        {
            var @object = CoreHub.ObjectManager.GetObjectByKey(Value);

            if (@object is null)
            {
                return [new UriTrailCrumb { Text = Value, Uri = Uri }];
            }

            var chain = ObjectHierarchy.GetVisibleAncestry(@object);
            var crumbs = new List<UriTrailCrumb>(chain.Count);

            foreach (var item in chain)
            {
                crumbs.Add(new UriTrailCrumb
                {
                    Text = item.Key,
                    Icon = ObjectIcon.Resolve(item),
                    Uri = CoreHub.ObjectKindManager.ResolveDetailUri(item) ?? (item == @object ? Uri : null)
                });
            }

            return crumbs;
        }
    }
}