using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WebPermission
{
    /// <summary>
    /// Reads the policies a permission dialog can grant from the ones the application registered.
    /// </summary>
    /// <remarks>
    /// The catalog is the running system's own list of <c>IIdentityPolicy</c> components, so it
    /// cannot fall behind the classes the guards check — which a table of policies maintained
    /// alongside them would.
    ///
    /// A policy belongs to the resource whose name it carries: the registered names follow
    /// <c>&lt;scope&gt;_&lt;role&gt;_policy</c>, so the dialog of a workspace offers the
    /// <c>workspace_…</c> policies rather than the whole catalog.
    ///
    /// <b>One resource administers a second one.</b> Objects carry no permissions of their own -
    /// who may see an object is decided by its security level, not by a grant on the record - so
    /// the <c>object_…</c> policies, which say who may read and change the objects of a data
    /// structure, are offered on the dialog of the <i>class</i>. That is the level an
    /// installation actually administers: a model in which every record had to be granted
    /// individually would be unusable. <see cref="Administered"/> holds the arrangement.
    /// </remarks>
    public static class PolicyCatalog
    {
        /// <summary>
        /// Returns the registered policies that apply to a resource.
        /// </summary>
        /// <param name="scope">The kind of resource, as named in <see cref="PermissionScope"/>.</param>
        /// <returns>
        /// The policy names, in the order they are offered. Empty when the application registered
        /// none for that resource.
        /// </returns>
        public static IEnumerable<string> GetPolicies(string scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                return [];
            }

            var prefixes = Prefixes(scope).ToList();

            return [.. GetRegisteredPolicies()
                .Where(x => prefixes.Any(p => x.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>
        /// Names the resources whose policies a dialog administers besides its own.
        /// </summary>
        /// <remarks>
        /// The class dialog administers the objects of the class. It is the only entry, and the
        /// only one there should be: the arrangement exists because a resource can be too
        /// numerous to grant individually, not as a general escape from the naming rule.
        /// </remarks>
        private static readonly IReadOnlyDictionary<string, string[]> Administered =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [PermissionScope.Class] = [PermissionScope.Object]
            };

        /// <summary>
        /// Returns the registered-name prefixes a dialog of the supplied scope offers.
        /// </summary>
        /// <param name="scope">The kind of resource the dialog belongs to.</param>
        /// <returns>The prefixes, the scope's own one first.</returns>
        private static IEnumerable<string> Prefixes(string scope)
        {
            yield return scope + "_";

            if (!Administered.TryGetValue(scope, out var others))
            {
                yield break;
            }

            foreach (var other in others)
            {
                yield return other + "_";
            }
        }

        /// <summary>
        /// Determines whether a policy is registered for a resource.
        /// </summary>
        /// <remarks>
        /// The dialog posts a name the client picked, so it is checked against the catalog before
        /// it is stored — a grant naming a policy no guard knows would never take effect and would
        /// sit in the list looking as though it had.
        /// </remarks>
        /// <param name="policy">The policy name to check.</param>
        /// <param name="scope">The kind of resource.</param>
        /// <returns>True when the policy is registered and applies to that resource.</returns>
        public static bool IsKnown(string policy, string scope)
        {
            return !string.IsNullOrWhiteSpace(policy) &&
                GetPolicies(scope).Contains(policy, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the label shown for a policy.
        /// </summary>
        /// <remarks>
        /// The registered name is a key rather than prose, so the role it names is turned into
        /// something readable: <c>workspace_admin_policy</c> reads as <c>Admin</c>. The resource is
        /// left out because the dialog already belongs to one.
        /// <para>
        /// A policy of a resource the dialog only <i>administers</i> keeps its resource in the
        /// label, because dropping it would make two different policies read the same: on the
        /// class dialog, <c>class_admin_policy</c> is <c>Admin</c> and
        /// <c>object_admin_policy</c> is <c>Object admin</c>. That falls out of stripping only
        /// the dialog's own prefix.
        /// </para>
        /// </remarks>
        /// <param name="policy">The registered policy name.</param>
        /// <param name="scope">The kind of resource.</param>
        /// <returns>The label of the policy.</returns>
        public static string GetLabel(string policy, string scope)
        {
            if (string.IsNullOrWhiteSpace(policy))
            {
                return policy;
            }

            var role = policy;
            var prefix = scope + "_";

            if (!string.IsNullOrWhiteSpace(scope) && role.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                role = role[prefix.Length..];
            }

            if (role.EndsWith("_policy", StringComparison.OrdinalIgnoreCase))
            {
                role = role[..^"_policy".Length];
            }

            role = role.Replace('_', ' ').Trim();

            return string.IsNullOrEmpty(role)
                ? policy
                : char.ToUpperInvariant(role[0]) + role[1..];
        }

        /// <summary>
        /// Resolves a stored policy name to the type that declares it, which is what the
        /// permission check needs: a grant records the name, while the registry answers questions
        /// about a policy by its type.
        /// </summary>
        /// <param name="policy">The registered policy name, as a grant stores it.</param>
        /// <returns>The policy type, or <see langword="null"/> when no policy carries that name.</returns>
        public static Type GetPolicyType(string policy)
        {
            if (string.IsNullOrWhiteSpace(policy))
            {
                return null;
            }

            var policies = CoreHub.ComponentHub?.IdentityManager?.Policies;

            if (policies is null)
            {
                return null;
            }

            return policies
                .Select(x => x.Policy)
                .FirstOrDefault(x => string.Equals(GetName(x), policy, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns the names of every policy the application registered.
        /// </summary>
        /// <remarks>
        /// The name is read from the policy type's <c>Name</c> attribute rather than from the
        /// registered component id, which is the full type name: it is the attribute value that
        /// the grants are stored under and that the rest of the system knows a policy by.
        ///
        /// Reported as empty rather than throwing when the host is not fully initialized, which is
        /// the case in unit tests that exercise the surrounding logic without a component hub.
        /// </remarks>
        /// <returns>The registered policy names.</returns>
        private static IEnumerable<string> GetRegisteredPolicies()
        {
            var policies = CoreHub.ComponentHub?.IdentityManager?.Policies;

            if (policies is null)
            {
                return [];
            }

            return [.. policies
                .Select(x => GetName(x.Policy))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>
        /// Returns the name a policy type is registered under.
        /// </summary>
        /// <remarks>
        /// The attribute discards its argument, so the value is taken from the attribute data the
        /// compiler recorded rather than from an instance of it.
        /// </remarks>
        /// <param name="policy">The policy type.</param>
        /// <returns>The registered name, or null when the type carries none.</returns>
        private static string GetName(Type policy)
        {
            var attribute = policy?.CustomAttributes
                .FirstOrDefault(x => x.AttributeType == typeof(NameAttribute));

            return attribute?.ConstructorArguments.FirstOrDefault().Value as string;
        }
    }
}
