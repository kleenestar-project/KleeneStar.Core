using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_
{
    /// <summary>
    /// Answers the availability check of the class name inputs: whether a name is still free
    /// among the classes of the workspace the route addresses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A class name is unique <b>per workspace</b>, not per installation. Two workspaces may
    /// both have an <c>Invoice</c> - the seeded finance and procurement workspaces do - and
    /// what a name has to be is unambiguous where it is used: in the class list of one
    /// workspace, and in the accepted-class lists of that workspace's relation types, which
    /// hold classes by name. The check therefore reads the workspace off the route rather
    /// than searching every class there is; a route that names no workspace answers
    /// <em>taken</em>, because a check that cannot say where it is looking cannot vouch for
    /// anything.
    /// </para>
    /// <para>
    /// The <c>exclude</c> query parameter names the class being edited, which never counts
    /// against itself: without it the edit dialog reported the record's own name as taken
    /// the moment the user typed it back in. It is advice for the form while it is being
    /// filled in - the gate is <see cref="Index.Validate"/>, which every caller passes.
    /// </para>
    /// </remarks>
    [Title("Workspace")]
    [Cache]
    public sealed partial class UniqueName : RestApiUnique
    {
        /// <summary>
        /// The name of the query parameter carrying the id of the class that is being edited.
        /// </summary>
        public const string ExcludeParameter = "exclude";

        /// <summary>
        /// Provides a regular expression that matches keys consisting of 1 to 64 non-control
        /// Unicode characters.
        /// </summary>
        [GeneratedRegex(@"^[\P{C}]{1,64}$")]
        private static partial Regex NameRegex();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public UniqueName()
        {
        }

        /// <summary>
        /// Determines whether the specified value is available based on the provided request context.
        /// </summary>
        /// <param name="value">
        /// The value to check for availability.
        /// </param>
        /// <param name="request">
        /// The request context containing additional information for the availability check.
        /// </param>
        /// <returns>True if the specified value is available; otherwise, false.</returns>
        protected override bool CheckAvailable(string value, Request request)
        {
            if (!NameRegex().IsMatch(value))
            {
                return false;
            }

            var workspaceKey = request?.GetParameter<WorkspaceKeyParameter>()?.Value;
            var workspace = CoreHub.WorkspaceManager?.GetWorkspaceByKey(workspaceKey);

            if (workspace is null)
            {
                return false;
            }

            var exclude = Guid.TryParse(request?.GetParameter(ExcludeParameter)?.Value, out var id)
                ? id
                : Guid.Empty;

            return IsAvailable(workspace.Id, value, exclude);
        }

        /// <summary>
        /// Determines whether a class name is free among the classes of a workspace.
        /// </summary>
        /// <remarks>
        /// The comparison ignores case and surrounding whitespace, the way the form and the
        /// validation of the CRUD endpoint compare: <c>Invoice</c> and <c>invoice</c> are one
        /// name, because nothing that resolves a class by name tells them apart.
        /// </remarks>
        /// <param name="workspaceId">The workspace the name is checked in.</param>
        /// <param name="name">The name to check.</param>
        /// <param name="exclude">The class that never counts against itself, or <see cref="Guid.Empty"/>.</param>
        /// <returns><see langword="true"/> when no other class of the workspace carries the name.</returns>
        public static bool IsAvailable(Guid workspaceId, string name, Guid exclude)
        {
            var candidate = name?.Trim();

            if (string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            var query = new Query<Model.Entities.Class>()
                .WhereEquals(x => x.WorkspaceId, workspaceId)
                .WhereEqualsIgnoreCase(x => x.Name, candidate);

            using var context = ModelHub.CreateDbContext();

            return CoreHub.ClassManager?
                .GetClasses(query, context)
                .Any(x => x.Id != exclude) != true;
        }
    }
}
