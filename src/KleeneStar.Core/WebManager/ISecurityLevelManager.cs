using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing security levels - the classifications a class knows -
    /// and for answering who is cleared to see an object carrying one.
    /// </summary>
    /// <remarks>
    /// A security level is defined per class, exactly the way a field is: the class is the
    /// catalog and an administrator decides what exists. What the level adds beyond a label is
    /// a clearance - the groups whose members may see, and assign, it.
    /// <para>
    /// <b>The rule is one sentence.</b> An object without a level is visible to everyone; an
    /// object with one is visible to an identity that belongs to at least one of the groups the
    /// level names. A level naming no group is closed, and so is a level nobody can resolve any
    /// more. This is deliberately the opposite reading of
    /// <see cref="IPermissionManager.IsGranted"/>, where an unadministered resource is
    /// unrestricted: creating a security level and putting it on an object <i>is</i> the act of
    /// administering, so there is no "nobody said anything" case left to interpret.
    /// </para>
    /// </remarks>
    public interface ISecurityLevelManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when a security level is added.
        /// </summary>
        event EventHandler<SecurityLevel> SecurityLevelAdded;

        /// <summary>
        /// An event that fires when a security level is updated.
        /// </summary>
        event EventHandler<SecurityLevel> SecurityLevelUpdated;

        /// <summary>
        /// An event that fires when a security level is removed.
        /// </summary>
        event EventHandler<SecurityLevel> SecurityLevelRemoved;

        /// <summary>
        /// Returns a security level based on its id.
        /// </summary>
        /// <param name="securityLevelId">The id of the security level.</param>
        /// <returns>The security level, or <c>null</c> when it does not exist.</returns>
        SecurityLevel GetSecurityLevel(Guid securityLevelId);

        /// <summary>
        /// Returns a security level based on its id.
        /// </summary>
        /// <param name="securityLevelId">The id of the security level.</param>
        /// <returns>The security level, or <c>null</c> when it does not exist.</returns>
        SecurityLevel GetSecurityLevel(SecurityLevelIdParameter securityLevelId);

        /// <summary>
        /// Retrieves the security levels defined on a class, ordered by rank.
        /// </summary>
        /// <param name="classId">The id of the class.</param>
        /// <returns>
        /// The security levels of the class. The collection is empty when the class defines
        /// none, which means every object of the class is unclassified.
        /// </returns>
        IEnumerable<SecurityLevel> GetSecurityLevels(ClassIdParameter classId);

        /// <summary>
        /// Retrieves the security levels that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned security levels. Must not be null.
        /// </param>
        /// <returns>The matching security levels, which may be empty.</returns>
        IEnumerable<SecurityLevel> GetSecurityLevels(IQuery<SecurityLevel> query);

        /// <summary>
        /// Retrieves the security levels that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned security levels. Must not be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Cannot be null.
        /// </param>
        /// <returns>The matching security levels, which may be empty.</returns>
        IEnumerable<SecurityLevel> GetSecurityLevels(IQuery<SecurityLevel> query, IQueryContext context);

        /// <summary>
        /// Returns the level an object of the class starts on, or <c>null</c> when the class
        /// names no default and a new object is therefore unclassified.
        /// </summary>
        /// <param name="classId">The id of the class.</param>
        /// <returns>The default security level, or <c>null</c>.</returns>
        SecurityLevel GetDefaultSecurityLevel(Guid classId);

        /// <summary>
        /// Returns the active levels of a class the supplied identity is cleared for, ordered
        /// by rank. These are the levels the identity may put on an object; offering a level
        /// somebody cannot see would let them file a record they immediately lose sight of.
        /// </summary>
        /// <param name="classId">The id of the class.</param>
        /// <param name="identityId">The identity the levels are offered to.</param>
        /// <returns>The assignable security levels, which may be empty.</returns>
        IReadOnlyList<SecurityLevel> GetAssignableSecurityLevels(Guid classId, Guid identityId);

        /// <summary>
        /// Determines whether an identity is cleared for a classification.
        /// </summary>
        /// <param name="identityId">The identity asking to see the object.</param>
        /// <param name="securityLevelId">
        /// The level the object carries, or <c>null</c> when the object is unclassified.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when the object may be shown: always for an unclassified
        /// object, and for a classified one when the identity belongs to a group the level
        /// names.
        /// </returns>
        bool IsCleared(Guid identityId, Guid? securityLevelId);

        /// <summary>
        /// Returns the ids of the levels the supplied identity is cleared for.
        /// </summary>
        /// <param name="identityId">The identity.</param>
        /// <returns>The cleared level ids, which may be empty.</returns>
        IReadOnlyCollection<Guid> GetClearedSecurityLevelIds(Guid identityId);

        /// <summary>
        /// Narrows an object query to what the supplied identity is cleared to see.
        /// </summary>
        /// <remarks>
        /// The narrowing is a predicate on the query rather than a filter over its result, so
        /// it is applied before paging and a page of hidden records does not come back as a
        /// short page. Returns the query untouched while an unrestricted scope is open.
        /// </remarks>
        /// <param name="query">The query to narrow. Must not be null.</param>
        /// <param name="identityId">The identity the query is run for.</param>
        /// <returns>The narrowed query.</returns>
        IQuery<Model.Entities.Object> Restrict(IQuery<Model.Entities.Object> query, Guid identityId);

        /// <summary>
        /// Suspends the classification filter for the duration of the scope.
        /// </summary>
        /// <remarks>
        /// The filter is applied by default, so a list added tomorrow is guarded without its
        /// author having to remember to guard it. What that costs is the handful of reads the
        /// system performs on its own behalf rather than on a user's - issuing the next object
        /// key, evaluating a relation guard, replaying a commit chain - which must see every
        /// record or they would answer wrongly. Those wrap themselves in this scope and say so.
        /// <para>
        /// Scopes nest and are ambient to the logical call, the way a commit scope is: an inner
        /// scope joins the outer one and only the outermost close restores the filter.
        /// </para>
        /// </remarks>
        /// <returns>The scope. Disposing it restores the filter.</returns>
        IDisposable BeginUnrestricted();

        /// <summary>
        /// Gets a value indicating whether an unrestricted scope is currently open.
        /// </summary>
        bool IsUnrestricted { get; }

        /// <summary>
        /// Adds a security level to the manager.
        /// </summary>
        /// <param name="securityLevelEntity">The security level to add. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        ISecurityLevelManager Add(SecurityLevel securityLevelEntity);

        /// <summary>
        /// Updates a security level in the manager.
        /// </summary>
        /// <param name="securityLevelEntity">The security level to update. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        ISecurityLevelManager Update(SecurityLevel securityLevelEntity);

        /// <summary>
        /// Removes the specified security level, declassifying every object that carried it.
        /// </summary>
        /// <param name="securityLevelId">The id of the security level to remove.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        ISecurityLevelManager Remove(Guid securityLevelId);
    }
}
