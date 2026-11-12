using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRelation;
using WebExpress.WebApp.WebRestApi;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Translates between the stored <see cref="ObjectRelation"/> and the generic
    /// <see cref="Relation"/> the framework's link surface reads, and resolves the two ends
    /// into the references that surface renders.
    /// </summary>
    /// <remarks>
    /// The two shapes differ in what they address by: the entity holds object ids, because
    /// that is what a foreign key can be, while the framework holds business keys, because
    /// that is what a person reads and what an external system could ever agree on. The
    /// translation therefore always passes through the object, which is also where the title,
    /// the class and the workflow state on a reference come from.
    /// </remarks>
    internal static class ObjectRelationProjection
    {
        /// <summary>
        /// Projects a stored relation onto the framework shape, read from the perspective of
        /// the object whose surface renders it.
        /// </summary>
        /// <param name="relation">The stored relation.</param>
        /// <returns>The framework relation, or <see langword="null"/> when the source is gone.</returns>
        public static Relation ToRelation(ObjectRelation relation)
        {
            if (relation?.SourceObject is null)
            {
                return null;
            }

            var projected = new Relation
            {
                Id = relation.Id.ToString(),
                System = relation.System,
                Type = relation.TypeKey,
                Direction = relation.Direction,
                Status = relation.Status,
                Source = ToReference(relation.SourceObject),
                Target = relation.TargetObject is not null
                    ? ToReference(relation.TargetObject)

                    // an external end has no object behind it, so it carries the address it
                    // was created with and the caption it was given
                    : new RelationReference
                    {
                        Uri = relation.TargetUri,
                        Title = relation.TargetTitle
                    },
                Comment = relation.Comment,
                Created = relation.Created,
                CreatedBy = relation.CreatedBy?.Name
            };

            foreach (var entry in relation.Metadata ?? [])
            {
                projected.Metadata[entry.Key] = entry.Value;
            }

            return projected;
        }

        /// <summary>
        /// Projects an object onto the reference the link surface renders for it: what it is
        /// called, what class it has, where it lives and which workflow state it is in.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <returns>The reference, or <see langword="null"/> when there is no object.</returns>
        public static RelationReference ToReference(ObjectEntity @object)
        {
            if (@object is null)
            {
                return null;
            }

            var (status, color) = ResolveStatus(@object);

            return new RelationReference
            {
                Key = @object.Key,
                Class = ClassNameOf(@object),
                Title = @object.Summary,
                Uri = CoreHub.ObjectKindManager.ResolveDetailUri(@object)?.ToString(),
                Status = status,
                StatusColor = color
            };
        }

        /// <summary>
        /// Projects an object onto the reference shape the target search answers with, which
        /// is the same reference the picked candidate becomes the target of.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <returns>The wire reference.</returns>
        public static RestApiRelationReference ToWireReference(ObjectEntity @object)
        {
            return RestApiRelationReference.From(ToReference(@object));
        }

        /// <summary>
        /// Returns the name of the class of an object, which is the token both ends of the
        /// hybrid model are reasoned about by: it is what a relation stores in its accepted
        /// class list, what the framework validates a target against, and what the surface
        /// prints beside a key.
        /// </summary>
        /// <remarks>
        /// The general object queries do not hydrate the class - most callers never look at
        /// it, and dragging the row along would cost every one of them - so the navigation
        /// property is only sometimes filled. The lookup by id is the fallback that keeps this
        /// projection correct whichever query the object arrived from.
        /// </remarks>
        /// <param name="object">The object.</param>
        /// <returns>The class name, or <see langword="null"/> when the class is gone.</returns>
        public static string ClassNameOf(ObjectEntity @object)
        {
            if (@object is null)
            {
                return null;
            }

            return @object.Class?.Name ?? CoreHub.ClassManager.GetClass(@object.ClassId)?.Name;
        }

        /// <summary>
        /// Reads the workflow state of an object as the link surface shows it: the caption of
        /// the status category its workflow field resolves to, and the semantic colour token
        /// that paints the dot in front of it.
        /// </summary>
        /// <remarks>
        /// The state is a snapshot for display; the referenced object stays authoritative. A
        /// class that models no workflow answers nothing rather than a placeholder, so the row
        /// simply carries no state instead of claiming an empty one.
        /// </remarks>
        /// <param name="object">The object.</param>
        /// <returns>The caption and its colour token, either of which may be null.</returns>
        private static (string Status, string Color) ResolveStatus(ObjectEntity @object)
        {
            var @class = @object.Class ?? CoreHub.ClassManager.GetClass(@object.ClassId);

            if (@class is null)
            {
                return (null, null);
            }

            var context = ObjectBoardProjection.BuildClassContext(@class);
            var categories = ObjectBoardProjection.GetOrderedCategories().ToDictionary(x => x.Id);
            var category = ObjectBoardProjection.ResolveCategory(@object.Id, context, categories);

            return category is null
                ? (null, null)
                : (ObjectBoardProjection.CategoryLabel(category), ColorToken(category));
        }

        /// <summary>
        /// Maps a status category onto one of the framework's contextual colour names. The
        /// token rather than a colour keeps the rendering themeable.
        /// </summary>
        /// <param name="category">The status category.</param>
        /// <returns>The colour token.</returns>
        private static string ColorToken(StatusCategory category)
        {
            return (category?.Name ?? string.Empty).Replace(" ", string.Empty).ToLowerInvariant() switch
            {
                "inprogress" => "info",
                "waiting" => "warning",
                "done" => "success",
                _ => "secondary"
            };
        }

        /// <summary>
        /// Resolves an object by its business key, which is how both ends of a relation are
        /// addressed on the wire.
        /// </summary>
        /// <param name="key">The business key.</param>
        /// <returns>The object, or <see langword="null"/> when the key names none.</returns>
        public static ObjectEntity ResolveObject(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? null : CoreHub.ObjectManager.GetObjectByKey(key);
        }

        /// <summary>
        /// Reads the object the current route addresses, which is the object whose relations
        /// the endpoint answers and the source of every relation it establishes.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The object, or <see langword="null"/> when the route addresses none.</returns>
        public static ObjectEntity ResolveSubject(WebExpress.WebCore.WebMessage.IRequest request)
        {
            return ResolveObject(request?.GetParameter<ObjectKeyParameter>()?.Value);
        }

        /// <summary>
        /// Builds the stored relation a framework relation describes, resolving both ends back
        /// into objects. It is the inverse of <see cref="ToRelation"/> and is what the create
        /// path persists.
        /// </summary>
        /// <param name="relation">The validated framework relation.</param>
        /// <param name="identityId">The identity establishing the relation.</param>
        /// <returns>The entity, or <see langword="null"/> when the source cannot be resolved.</returns>
        public static ObjectRelation ToEntity(Relation relation, Guid? identityId)
        {
            var source = ResolveObject(relation?.Source?.Key);

            if (source is null)
            {
                return null;
            }

            var target = ResolveObject(relation.Target?.Key);

            var entity = new ObjectRelation
            {
                Id = Guid.NewGuid(),
                System = relation.System,
                TypeKey = relation.Type,
                Direction = relation.Direction,
                Status = relation.Status,
                SourceObjectId = source.Id,
                TargetObjectId = target?.Id,

                // an object end derives its address from its own route, so only an external
                // end stores one
                TargetUri = target is null ? relation.Target?.Uri : null,
                TargetTitle = target is null ? relation.Target?.Title : null,
                Comment = relation.Comment,
                CreatedById = identityId == Guid.Empty ? null : identityId,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };

            foreach (var entry in relation.Metadata ?? new Dictionary<string, string>())
            {
                entity.Metadata[entry.Key] = entry.Value;
            }

            return entity;
        }
    }
}
