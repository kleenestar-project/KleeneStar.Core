using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WWW.Api._1_.Tags._objectkey_
{
    /// <summary>
    /// Serves the labels of one object to the framework's tag control: the labels it carries
    /// (<c>GET</c>), suggestions from the labels already used in its workspace
    /// (<c>GET ?q=</c>), adding one (<c>POST</c>) and removing one (<c>DELETE {value}</c>).
    /// </summary>
    /// <remarks>
    /// The object is read through the object manager, so an object the caller may not read has
    /// no labels to show and none to change. Adding and removing is changing the object and asks
    /// for <c>ObjectUpdatePermission</c> on its chain; a refusal travels as the failure the tag
    /// control already reports (the framework answers it as 400), because the base's entry
    /// points are what the control calls.
    /// </remarks>
    [ObjectKeySegment]
    [IncludeSubPaths(true)]
    [Cache]
    public sealed class Index : RestApiTag
    {
        /// <summary>
        /// How many suggestions the autocomplete offers.
        /// </summary>
        private const int MaxSuggestions = 10;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Returns the labels the object carries, in the order they were added.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The labels.</returns>
        protected override IEnumerable<RestApiTagItem> RetrieveTags(IRequest request)
        {
            var @object = ResolveObject(request);

            return @object is null
                ? []
                : CoreHub.ObjectTagManager.GetTags(@object.Id).Select(ToItem).ToList();
        }

        /// <summary>
        /// Suggests the labels already used on the readable objects of the object's workspace
        /// that contain the term and that the object does not carry yet - a vocabulary grows
        /// out of what people wrote, not out of a list somebody has to keep.
        /// </summary>
        /// <param name="term">The search term.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The suggestions.</returns>
        protected override IEnumerable<RestApiTagItem> SuggestTags(string term, IRequest request)
        {
            var @object = ResolveObject(request);

            if (@object is null)
            {
                return [];
            }

            var own = CoreHub.ObjectTagManager.GetTags(@object.Id)
                .Select(x => x.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var readable = CoreHub.ObjectManager
                .GetObjects(new Query<ObjectEntity>().WhereEquals(x => x.WorkspaceId, @object.WorkspaceId))
                .Select(x => x.Id)
                .ToHashSet();

            return [.. CoreHub.ObjectTagManager
                .GetTags(new Query<Model.Entities.ObjectTag>())
                .Where(x => readable.Contains(x.ObjectId)
                    && !own.Contains(x.Name)
                    && x.Name.Contains(term.Trim(), StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase)
                .Take(MaxSuggestions)
                .Select(x => ToItem(x.First()))];
        }

        /// <summary>
        /// Adds a label, once the caller may change the object.
        /// </summary>
        /// <param name="payload">The label to add.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The label, or <see langword="null"/> when the object is not there.</returns>
        protected override RestApiTagItem CreateTag(RestApiTagPayload payload, IRequest request)
        {
            var @object = ResolveWritable(request);
            var name = payload?.Value?.Trim();

            if (@object is null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var tag = CoreHub.ObjectTagManager.Add(@object.Id, name, ObjectTagBadge.DeriveColor(name));

            return tag is null ? null : ToItem(tag);
        }

        /// <summary>
        /// Removes a label by its name, once the caller may change the object.
        /// </summary>
        /// <param name="value">The name of the label.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when a label was removed.</returns>
        protected override bool DeleteTag(string value, IRequest request)
        {
            var @object = ResolveWritable(request);
            var name = Uri.UnescapeDataString(value ?? string.Empty);

            var tag = @object is null
                ? null
                : CoreHub.ObjectTagManager.GetTags(@object.Id)
                    .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

            return tag is not null && CoreHub.ObjectTagManager.Remove(tag.Id);
        }

        /// <summary>
        /// Resolves the object the route names, as the caller may read it.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The object, or <see langword="null"/>.</returns>
        private static ObjectEntity ResolveObject(IRequest request)
        {
            return CoreHub.ObjectManager.GetObjectByKey(request?.GetParameter<ObjectKeyParameter>()?.Value);
        }

        /// <summary>
        /// Resolves the object the route names and refuses a caller who may not change it.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The object, or <see langword="null"/> when it is not there.</returns>
        /// <exception cref="UnauthorizedAccessException">The caller may not change the object.</exception>
        private static ObjectEntity ResolveWritable(IRequest request)
        {
            var @object = ResolveObject(request);

            if (@object is not null && !ContentAuthorization.MayWrite(@object, request))
            {
                throw new UnauthorizedAccessException("The labels of this object may be changed by those who may change it only.");
            }

            return @object;
        }

        /// <summary>
        /// Projects a stored label onto the tag control's item.
        /// </summary>
        /// <param name="tag">The label.</param>
        /// <returns>The item.</returns>
        private static RestApiTagItem ToItem(Model.Entities.ObjectTag tag)
        {
            return new RestApiTagItem
            {
                Value = tag.Name,
                Color = string.IsNullOrWhiteSpace(tag.Color) ? ObjectTagBadge.DeriveColor(tag.Name) : tag.Color
            };
        }
    }
}
