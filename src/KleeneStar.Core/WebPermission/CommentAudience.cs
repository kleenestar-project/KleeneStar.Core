using KleeneStar.Core.WebPermissions;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebPermission
{
    /// <summary>
    /// Answers who a comment reaches - the one rule every surface that shows, counts or changes
    /// comments asks, the operator thread and the customer portal alike.
    /// </summary>
    /// <remarks>
    /// A <see cref="CommentVisibility.Public"/> comment reaches everyone who may read its object.
    /// A <see cref="CommentVisibility.InternalTeam"/> comment is the service team's working
    /// note and reaches the <em>service team</em> alone: an internal account (one that belongs
    /// to no tenant) that may read the object. An account with a tenant is a customer's - the
    /// requester, a colleague the issue was shared with, the client of the order - and never
    /// reads an internal note, whatever its groups are granted. Granting a customer group read
    /// access to a workspace must not hand them the team's notes about their own request.
    /// <para>
    /// The requester used to be part of the internal audience, which made "internal" mean
    /// "everyone but the colleagues it was shared with". A note the customer can read is a
    /// public comment; the visibility exists to say the opposite.
    /// </para>
    /// <para>
    /// The rule is answered for the caller of the call chain (the request being served, or a
    /// <c>BeginIdentity</c> scope). With nobody acting - start-up, background work - the system
    /// reads every comment, the way <see cref="ContentVisibility"/> narrows nothing then.
    /// </para>
    /// </remarks>
    public static class CommentAudience
    {
        /// <summary>
        /// Determines whether the current caller reads the internal notes of an object.
        /// </summary>
        /// <param name="object">The object the comments hang on.</param>
        /// <returns><see langword="true"/> when internal comments reach the caller.</returns>
        public static bool SeesInternal(ObjectEntity @object)
        {
            if (CoreHub.SessionManager?.HasCaller != true)
            {
                return true;
            }

            return IsServiceTeam(@object, CoreHub.SessionManager.GetCurrentIdentityId(null), typeof(ObjectReadPermission));
        }

        /// <summary>
        /// Determines whether an identity reads the internal notes of an object.
        /// </summary>
        /// <param name="object">The object the comments hang on.</param>
        /// <param name="identityId">The reader.</param>
        /// <returns><see langword="true"/> when internal comments reach the identity.</returns>
        public static bool SeesInternal(ObjectEntity @object, Guid identityId)
        {
            return IsServiceTeam(@object, identityId, typeof(ObjectReadPermission));
        }

        /// <summary>
        /// Determines whether the current caller may write an internal note on an object.
        /// </summary>
        /// <remarks>
        /// A customer cannot address a note to an audience they are not part of - they would
        /// not see what they wrote - so the composer offers the internal audience to the
        /// service team only, and the endpoints refuse it from anybody else.
        /// </remarks>
        /// <param name="object">The object to comment on.</param>
        /// <returns><see langword="true"/> when the caller may write an internal note.</returns>
        public static bool MayWriteInternal(ObjectEntity @object)
        {
            if (CoreHub.SessionManager?.HasCaller != true)
            {
                return true;
            }

            return IsServiceTeam(@object, CoreHub.SessionManager.GetCurrentIdentityId(null), typeof(ObjectCommentPermission));
        }

        /// <summary>
        /// Determines whether a comment reaches a reader who was already told whether internal
        /// notes reach them.
        /// </summary>
        /// <param name="comment">The comment.</param>
        /// <param name="seesInternal">The answer of <see cref="SeesInternal(ObjectEntity)"/> for
        /// the reader and the comment's object, computed once per thread.</param>
        /// <returns><see langword="true"/> when the comment is shown.</returns>
        public static bool Reaches(Comment comment, bool seesInternal)
        {
            return comment is not null
                && (seesInternal || comment.Visibility != CommentVisibility.InternalTeam);
        }

        /// <summary>
        /// Narrows the comments of an object to those that reach the current caller - for a
        /// count or a teaser, which must not say more than the thread would show.
        /// </summary>
        /// <param name="object">The object the comments hang on.</param>
        /// <param name="comments">The comments of the object.</param>
        /// <returns>The comments the caller reads.</returns>
        public static IEnumerable<Comment> Filter(ObjectEntity @object, IEnumerable<Comment> comments)
        {
            var seesInternal = SeesInternal(@object);

            return (comments ?? []).Where(x => Reaches(x, seesInternal));
        }

        /// <summary>
        /// Determines whether the current caller reads a single comment - for the paths that
        /// address a comment by id (edit, delete, reply, like, pin) and must not act on a note
        /// the caller was never shown.
        /// </summary>
        /// <param name="comment">The comment.</param>
        /// <returns><see langword="true"/> when the comment reaches the caller.</returns>
        public static bool Reaches(Comment comment)
        {
            if (comment is null)
            {
                return false;
            }

            if (comment.Visibility != CommentVisibility.InternalTeam)
            {
                return true;
            }

            return SeesInternal(CoreHub.ObjectManager.GetObject(comment.ObjectId));
        }

        /// <summary>
        /// Determines whether an identity is part of the service team of an object.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="identityId">The identity.</param>
        /// <param name="permission">The permission the identity must hold on the object's chain.</param>
        /// <returns><see langword="true"/> for an active internal account holding the permission.</returns>
        private static bool IsServiceTeam(ObjectEntity @object, Guid identityId, Type permission)
        {
            if (@object is null || identityId == Guid.Empty)
            {
                return false;
            }

            var identity = CoreHub.IdentityManager?.GetIdentity(identityId);

            if (identity is null || identity.State != IdentityState.Active || identity.TenantId is not null)
            {
                return false;
            }

            return CoreHub.PermissionManager?.IsGranted(identityId, permission, PageAuthorization.ChainOf(@object)) ?? true;
        }
    }
}
