using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebPermission;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebReport
{
    using ObjectEntity = KleeneStar.Model.Entities.Object;

    /// <summary>
    /// A relation of an exported object, seen from the object.
    /// </summary>
    /// <param name="Type">The label of the relation type in the direction seen from the object.</param>
    /// <param name="Key">The key of the other object, or <see langword="null"/> for an external address.</param>
    /// <param name="Title">The summary of the other object, or the caption of the external address.</param>
    /// <param name="Class">The class name of the other object, or <see langword="null"/>.</param>
    /// <param name="Status">The translated relation status.</param>
    /// <param name="Uri">The external address, or <see langword="null"/> for an object.</param>
    public sealed record ObjectExportRelation(string Type, string Key, string Title, string Class, string Status, string Uri);

    /// <summary>
    /// A comment of an exported object.
    /// </summary>
    /// <param name="Author">The author name.</param>
    /// <param name="Created">The time of writing (UTC).</param>
    /// <param name="Internal">Whether the comment is an internal note.</param>
    /// <param name="Reply">Whether the comment answers another comment.</param>
    /// <param name="Content">The content as stored (editor document or text).</param>
    /// <param name="Text">The content as plain text.</param>
    public sealed record ObjectExportComment(string Author, DateTime Created, bool Internal, bool Reply, string Content, string Text);

    /// <summary>
    /// Collects the relations and comments of an object export.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Relations: <see cref="IObjectRelationManager.GetRelations(Guid)"/>; the other end is read
    /// again through <see cref="IObjectManager.GetObject(Guid)"/>, so a relation to an object the
    /// caller may not read is not exported. The type label is the inverse label when the object
    /// is the target of an asymmetric type.
    /// </para>
    /// <para>
    /// Comments: <see cref="ICommentManager.GetComments(Guid)"/> narrowed by
    /// <see cref="CommentAudience.Filter"/> (internal notes only for the service team), deleted
    /// comments omitted, in thread order (a reply after the comment it answers).
    /// </para>
    /// </remarks>
    public static partial class ObjectExport
    {
        /// <summary>
        /// Collects the relations of an object the caller may read the other end of.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="request">The request, carrying the culture.</param>
        /// <returns>The relations.</returns>
        private static List<ObjectExportRelation> CollectRelations(ObjectEntity @object, IRequest request)
        {
            var relations = new List<ObjectExportRelation>();

            foreach (var relation in CoreHub.ObjectRelationManager.GetRelations(@object.Id) ?? [])
            {
                var outgoing = relation.SourceObjectId == @object.Id;
                var type = CoreHub.ObjectRelationTypeManager.GetRelationType(relation.TypeKey);
                var label = type is null
                    ? relation.TypeKey
                    : outgoing || type.Symmetric || string.IsNullOrWhiteSpace(type.InverseLabel) ? type.Label : type.InverseLabel;
                var status = I18N.Translate(request, "kleenestar.core:object.export.relation.status." + relation.Status.ToString().ToLowerInvariant());
                var otherId = outgoing ? relation.TargetObjectId : relation.SourceObjectId;

                if (otherId is null)
                {
                    relations.Add(new ObjectExportRelation(I18N.Translate(request, label), null, relation.TargetTitle ?? relation.TargetUri, null, status, relation.TargetUri));

                    continue;
                }

                var other = CoreHub.ObjectManager.GetObject(otherId.Value);

                if (other is null)
                {
                    continue;
                }

                relations.Add(new ObjectExportRelation
                (
                    I18N.Translate(request, label),
                    other.Key,
                    other.Summary,
                    CoreHub.ClassManager.GetClass(other.ClassId)?.Name,
                    status,
                    null
                ));
            }

            return relations;
        }

        /// <summary>
        /// Collects the comments of an object that reach the caller, in thread order.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <returns>The comments.</returns>
        private static List<ObjectExportComment> CollectComments(ObjectEntity @object)
        {
            var comments = CommentAudience.Filter(@object, CoreHub.CommentManager.GetComments(@object.Id))
                .Where(x => x.State != CommentState.Deleted && x.DeletedAt is null)
                .ToList();
            var ids = comments.Select(x => x.Id).ToHashSet();
            var replies = comments
                .Where(x => x.ParentCommentId is Guid parent && ids.Contains(parent))
                .ToLookup(x => x.ParentCommentId.Value);
            var result = new List<ObjectExportComment>();

            void Visit(Comment comment, bool reply)
            {
                result.Add(new ObjectExportComment
                (
                    comment.Author?.Name ?? CoreHub.IdentityManager.GetIdentity(comment.AuthorId)?.Name,
                    comment.Created,
                    comment.Visibility == CommentVisibility.InternalTeam,
                    reply,
                    comment.Content,
                    ProseText.ToPlainText(comment.Content)
                ));

                foreach (var answer in replies[comment.Id].OrderBy(x => x.Created))
                {
                    Visit(answer, true);
                }
            }

            foreach (var top in comments
                .Where(x => x.ParentCommentId is not Guid parent || !ids.Contains(parent))
                .OrderBy(x => x.Created))
            {
                Visit(top, false);
            }

            return result;
        }
    }
}
