using System;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Answers how far an object has come, rolling the answer up from the objects it aggregates.
    /// </summary>
    /// <remarks>
    /// The relation model has always been able to say that one object reports the progress of
    /// others - that is what <see cref="WebExpress.WebApp.WebRelation.RelationEffect.AggregatesProgress"/>
    /// declares - and until now nothing computed it: a parent showed the progress of its own
    /// state, which for a container is nearly meaningless, because a container is never itself
    /// <em>in progress</em>, only its children are.
    /// <para>
    /// It reads and never writes. A rolled-up percentage is not stored on the parent: it is a
    /// statement about the children as they are now, and a stored copy would be wrong from the
    /// next transition onwards without anything saying so.
    /// </para>
    /// </remarks>
    public interface IObjectProgressManager : IComponentManager
    {
        /// <summary>
        /// Returns how far the supplied object has come.
        /// </summary>
        /// <remarks>
        /// An object that aggregates nothing answers with the progress of its own workflow
        /// state - the reading the plan views have always used. An object that aggregates
        /// reports the average of what it aggregates, each child counted at its own rolled-up
        /// percentage, so a tree is summarised bottom-up.
        /// </remarks>
        /// <param name="objectId">The object to measure.</param>
        /// <returns>The progress. An object that cannot be read - it is gone, or the caller is
        /// not cleared for it - answers zero rather than an error.</returns>
        ObjectProgress GetProgress(Guid objectId);
    }
}
