using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Runs recursive operations on a subtree - archive (always with the contents), restore
    /// (exactly what an archive run archived), delete (children before parents), dissolve (the
    /// children to the parent, then the emptied object) and clone (the object alone or with
    /// its contents) - object by object through the object manager, as resumable operation runs
    /// (<c>kleenestar.hierarchy.md</c>, recursive operations).
    /// </summary>
    public interface IObjectOperationManager : IComponentManager
    {
        /// <summary>
        /// Works out what an operation would do, without doing it: the objects in processing
        /// order, how many of the subtree the caller may not see or not change, and the reason
        /// it would be refused.
        /// </summary>
        /// <param name="kind">The kind (<see cref="ObjectOperationKinds"/>).</param>
        /// <param name="rootId">The object the operation starts from.</param>
        /// <param name="mode">The option of the kind (clone: root, contents).</param>
        /// <param name="mayWrite">Whether the caller may change an object.</param>
        /// same-named counterpart of the root); ignored by the other kinds.</param>
        /// <returns>The plan.</returns>
        ObjectOperationPlan Plan(string kind, Guid rootId, string mode, Func<Model.Entities.Object, bool> mayWrite);

        /// <summary>
        /// Starts an operation: refused when the plan is, otherwise recorded as a run and processed
        /// at once when it is small (<see cref="HierarchySettings.InlineLimit"/>), else left to the
        /// worker. A key already used answers the run it started.
        /// </summary>
        /// <param name="kind">The kind.</param>
        /// <param name="rootId">The root.</param>
        /// <param name="mode">The option of the kind.</param>
        /// <param name="key">The idempotency key, or <see langword="null"/> for a new one.</param>
        /// <param name="identityId">The identity on whose behalf the run acts.</param>
        /// <param name="mayWrite">Whether that identity may change an object.</param>
        /// <returns>The run, or the refusal.</returns>
        ObjectOperationStart Start(string kind, Guid rootId, string mode, string key, Guid identityId, Func<Model.Entities.Object, bool> mayWrite);

        /// <summary>
        /// Returns a run by id or by key.
        /// </summary>
        /// <param name="idOrKey">The id or the key.</param>
        /// <returns>The run, or <see langword="null"/>.</returns>
        ObjectOperation GetOperation(string idOrKey);

        /// <summary>
        /// Advances the runs waiting for the worker (and runs a crash left behind), at most
        /// <paramref name="budget"/> objects.
        /// </summary>
        /// <param name="budget">The most objects processed in this call.</param>
        /// <returns>The number of objects processed.</returns>
        int ProcessDue(int budget = 500);

        /// <summary>
        /// Works out a transfer into another workspace without doing it: the objects, and the
        /// mapping of classes, fields, workflow states and security levels the caller sees (and
        /// may adjust) before anything changes.
        /// </summary>
        /// <param name="rootId">The object to move (with its subtree).</param>
        /// <param name="options">The target and the mapping.</param>
        /// <param name="mayWrite">Whether the caller may change an object of the source.</param>
        /// <param name="mayCreateIn">Whether the caller may create objects of a target class.</param>
        /// <returns>The plan, with <see cref="ObjectOperationPlan.Transfer"/>.</returns>
        ObjectOperationPlan PlanTransfer(Guid rootId, TransferOptions options, Func<Model.Entities.Object, bool> mayWrite, Func<Guid, bool> mayCreateIn);

        /// <summary>
        /// Starts a transfer into another workspace.
        /// </summary>
        /// <param name="rootId">The object to move (with its subtree).</param>
        /// <param name="options">The target and the mapping.</param>
        /// <param name="key">The idempotency key, or <see langword="null"/>.</param>
        /// <param name="identityId">The identity on whose behalf the run acts.</param>
        /// <param name="mayWrite">Whether that identity may change an object of the source.</param>
        /// <param name="mayCreateIn">Whether that identity may create objects of a target class.</param>
        /// <returns>The run, or the refusal.</returns>
        ObjectOperationStart StartTransfer(Guid rootId, TransferOptions options, string key, Guid identityId, Func<Model.Entities.Object, bool> mayWrite, Func<Guid, bool> mayCreateIn);
    }

    /// <summary>
    /// The target and mapping of a transfer.
    /// </summary>
    /// <param name="TargetWorkspaceId">The workspace to move into (the object lands at its root).</param>
    /// <param name="ClassMap">Source class to target class, where the caller chose one; the rest map by name.</param>
    /// <param name="Declassify">Whether an object whose security level has no counterpart may lose it.</param>
    public sealed record TransferOptions(Guid TargetWorkspaceId, IReadOnlyDictionary<Guid, Guid> ClassMap, bool Declassify);

    /// <summary>
    /// The mapping of a planned transfer.
    /// </summary>
    /// <param name="Classes">One entry per source class.</param>
    /// <param name="DroppedFields">The fields the target classes lack (their values are dropped).</param>
    /// <param name="States">The workflow states as they will be mapped.</param>
    /// <param name="Levels">The security levels and their counterparts.</param>
    public sealed record TransferPlan(IReadOnlyList<TransferClass> Classes, IReadOnlyList<TransferField> DroppedFields, IReadOnlyList<TransferState> States, IReadOnlyList<TransferLevel> Levels);

    /// <summary>
    /// A source class and its target.
    /// </summary>
    public sealed record TransferClass(Guid SourceId, string SourceName, Guid? TargetId, string TargetName, IReadOnlyList<TransferCandidate> Candidates);

    /// <summary>
    /// A class of the target workspace a source class may map to.
    /// </summary>
    public sealed record TransferCandidate(Guid Id, string Name);

    /// <summary>
    /// A field whose values a transfer drops.
    /// </summary>
    public sealed record TransferField(string ClassName, string FieldName);

    /// <summary>
    /// A workflow state and the state it maps to (<see langword="null"/>: the target has no workflow).
    /// </summary>
    public sealed record TransferState(string ClassName, string From, string To);

    /// <summary>
    /// A security level and its counterpart (<see langword="null"/>: none - refused unless declassified).
    /// </summary>
    public sealed record TransferLevel(string Name, Guid? TargetId);

    /// <summary>
    /// What an operation would do.
    /// </summary>
    /// <param name="Kind">The kind.</param>
    /// <param name="Root">The root, read unrestricted.</param>
    /// <param name="Items">The objects in processing order.</param>
    /// <param name="Subtree">The number of objects below the root.</param>
    /// <param name="Hidden">How many of them the caller may not see.</param>
    /// <param name="Unwritable">How many visible ones the caller may not change.</param>
    /// <param name="RefusalKey">The i18n key of the refusal, or <see langword="null"/>.</param>
    /// <param name="RefusalArguments">The arguments of the refusal.</param>
    /// <param name="ReferenceId">A run the operation refers to (a restore: the archive run).</param>
    public sealed record ObjectOperationPlan
    (
        string Kind,
        Model.Entities.Object Root,
        IReadOnlyList<Model.Entities.Object> Items,
        int Subtree,
        int Hidden,
        int Unwritable,
        string RefusalKey,
        object[] RefusalArguments,
        Guid? ReferenceId = null,
        TransferPlan Transfer = null
    )
    {
        /// <summary>
        /// Gets whether the plan is refused.
        /// </summary>
        public bool Refused => RefusalKey is not null;
    }

    /// <summary>
    /// The outcome of <see cref="IObjectOperationManager.Start"/>: the run, or why there is none.
    /// </summary>
    /// <param name="Operation">The run.</param>
    /// <param name="Plan">The plan it was started from.</param>
    public sealed record ObjectOperationStart(ObjectOperation Operation, ObjectOperationPlan Plan);
}
