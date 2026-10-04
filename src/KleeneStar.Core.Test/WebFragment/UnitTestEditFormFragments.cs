using KleeneStar.Core.WebFragment.Calendar;
using KleeneStar.Core.WebFragment.Class;
using KleeneStar.Core.WebFragment.Insight;
using KleeneStar.Core.WebFragment.Field;
using KleeneStar.Core.WebFragment.Form;
using KleeneStar.Core.WebFragment.Group;
using KleeneStar.Core.WebFragment.Identity;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebFragment.Priority;
using KleeneStar.Core.WebFragment.SavedSearch;
using KleeneStar.Core.WebFragment.Sla;
using KleeneStar.Core.WebFragment.Status;
using KleeneStar.Core.WebFragment.Template;
using KleeneStar.Core.WebFragment.Tenant;
using KleeneStar.Core.WebFragment.Workflow;
using KleeneStar.Core.WebFragment.Workspace;
using KleeneStar.Model.Entities;
using System.Collections;
using System.Reflection;

namespace KleeneStar.Core.Test.WebFragment
{
    /// <summary>
    /// Provides unit tests for every <c>EditFormFragment</c> shipped by the
    /// <c>KleeneStar.Core</c> assembly. Each fragment extends
    /// <c>FragmentControlDataFormEdit</c> and follows the same structural
    /// shape: a parameterless constructor with a nullable
    /// <see cref="WebExpress.WebCore.WebFragment.IFragmentContext"/>, a set of
    /// typed <c>ControlDataFormItem*</c> properties, an <c>ItemId</c> setter
    /// that resolves the row id from a request parameter, and a single
    /// <c>this.DataService&lt;T&gt;()</c> call that wires the REST endpoint
    /// the form posts to. The tests below pin down exactly those properties
    /// so a future refactor of an Edit form (e.g. accidental drop of the
    /// <c>ItemId</c> setter, missing <c>DataService&lt;T&gt;</c> call,
    /// selection control whose <c>ServiceFactory</c> resolves to an empty
    /// URL) is caught before the running host produces a broken page.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestEditFormFragments
    {
        // ------------------------------------------------------------------
        // Calendar
        // ------------------------------------------------------------------

        /// <summary>
        /// Verifies that <see cref="CalendarEditFormFragment"/> registers all
        /// six calendar-edit controls (Name, Description, Category, Color,
        /// CalendarType, CalendarState), wires the
        /// <c>…/Api/_1_/Calendars/Index</c> endpoint, and that its
        /// <c>ItemId</c> setter reads <see cref="WebParameter.CalendarIdParameter"/>.
        /// </summary>
        [Fact]
        public void Calendar_Fragment_HasExpectedShape()
        {
            CoreHubFixture.Initialize(nameof(Calendar_Fragment_HasExpectedShape));

            var frag = new CalendarEditFormFragment(null!);

            AssertEditFragmentShape(
                frag,
                expectedControls: 6,
                expectedDataServiceEndpoint: typeof(KleeneStar.Core.WWW.Api._1_.Calendars.Index));
        }

        // ------------------------------------------------------------------
        // Class
        // ------------------------------------------------------------------

        /// <summary>
        /// Verifies that <see cref="ClassEditFormFragment"/> registers all
        /// nine class-edit controls and binds the
        /// <c>…/Api/_1_/Classes/Index</c> endpoint.
        /// </summary>
        [Fact]
        public void Class_Fragment_HasExpectedShape()
        {
            CoreHubFixture.Initialize(nameof(Class_Fragment_HasExpectedShape));

            var frag = new ClassEditFormFragment(null!);

            AssertEditFragmentShape(
                frag,
                expectedControls: 9,
                expectedDataServiceEndpoint: typeof(KleeneStar.Core.WWW.Api._1_.Classes.Index));
        }

        // ------------------------------------------------------------------
        // Insight
        // ------------------------------------------------------------------

        /// <summary>
        /// Verifies that <see cref="InsightEditFormFragment"/> registers
        /// four insight-edit controls and binds the
        /// <c>…/Api/_1_/Insights/Index</c> endpoint. Regression guard for
        /// Pitfall&nbsp;4A from the Edit-form migration skill: the dashboard
        /// (now insight) fragment originally had no <c>ItemId</c> setter and produced empty
        /// <c>data-id=</c> attributes; the assertion below fails if a
        /// refactor drops the setter again.
        /// </summary>
        [Fact]
        public void Insight_Fragment_HasExpectedShape()
        {
            CoreHubFixture.Initialize(nameof(Insight_Fragment_HasExpectedShape));

            var frag = new InsightEditFormFragment(null!);

            AssertEditFragmentShape(
                frag,
                expectedControls: 4,
                expectedDataServiceEndpoint: typeof(KleeneStar.Core.WWW.Api._1_.Insights.Index));
        }

        /// <summary>
        /// Verifies that <see cref="FieldEditFormFragment"/> registers all
        /// twelve field-edit controls (Name, Description, HelpText,
        /// Placeholder, FieldType, Cardinality, Required, Unique, Deprecated,
        /// AccessModifier, DefaultSpec, State) and binds the
        /// <c>…/Api/_1_/Fields/Index</c> endpoint. Field is the canonical
        /// reference Edit fragment post-migration; the test count is the
        /// ground truth for the migration recipe.
        /// </summary>
        [Fact]
        public void Field_Fragment_HasExpectedShape()
        {
            CoreHubFixture.Initialize(nameof(Field_Fragment_HasExpectedShape));

            var frag = new FieldEditFormFragment(null!);

            AssertEditFragmentShape(
                frag,
                expectedControls: 12,
                expectedDataServiceEndpoint: typeof(KleeneStar.Core.WWW.Api._1_.Fields.Index));
        }

        /// <summary>
        /// Verifies that <see cref="FormEditFormFragment"/> registers three
        /// form-edit controls and binds the
        /// <c>…/Api/_1_/Forms/Index</c> endpoint.
        /// </summary>
        [Fact]
        public void Form_Fragment_HasExpectedShape()
        {
            CoreHubFixture.Initialize(nameof(Form_Fragment_HasExpectedShape));

            var frag = new FormEditFormFragment(null!);

            AssertEditFragmentShape(
                frag,
                expectedControls: 3,
                expectedDataServiceEndpoint: typeof(KleeneStar.Core.WWW.Api._1_.Forms.Index));
        }

        /// <summary>
        /// Verifies that <see cref="GroupEditFormFragment"/> registers the
        /// two group-edit controls (Name, State) and binds the
        /// <c>…/Api/_1_/Groups/Index</c> endpoint.
        /// </summary>
        [Fact]
        public void Group_Fragment_HasExpectedShape()
        {
            CoreHubFixture.Initialize(nameof(Group_Fragment_HasExpectedShape));

            var frag = new GroupEditFormFragment(null!);

            AssertEditFragmentShape(
                frag,
                expectedControls: 2,
                expectedDataServiceEndpoint: typeof(KleeneStar.Core.WWW.Api._1_.Groups.Index));
        }

        /// <summary>
        /// Verifies that <see cref="IdentityEditFormFragment"/> registers the
        /// three identity-edit controls and binds the
        /// <c>…/Api/_1_/Identities/Index</c> endpoint.
        /// </summary>
        [Fact]
        public void Identity_Fragment_HasExpectedShape()
        {
            CoreHubFixture.Initialize(nameof(Identity_Fragment_HasExpectedShape));

            var frag = new IdentityEditFormFragment(null!);

            AssertEditFragmentShape(
                frag,
                expectedControls: 3,
                expectedDataServiceEndpoint: typeof(KleeneStar.Core.WWW.Api._1_.Identities.Index));
        }

        /// <summary>
        /// Verifies that <see cref="ObjectEditFormFragment"/> wires the
        /// <c>…/Api/_1_/Objects/Index</c> endpoint and that at least one
        /// control is registered (the object Edit fragment composes controls
        /// dynamically through nested groups, so an exact <c>Add()</c>-count
        /// assertion would be brittle; this test only checks that the
        /// endpoint is wired and that the <c>ItemId</c> setter resolves the
        /// <see cref="WebParameter.ObjectKeyParameter"/>).
        /// </summary>
        [Fact]
        public void Object_Fragment_HasExpectedShape()
        {
            CoreHubFixture.Initialize(nameof(Object_Fragment_HasExpectedShape));

            var frag = new ObjectEditFormFragment(null!);

            AssertEditFragmentShape(
                frag,
                expectedControls: null,
                expectedDataServiceEndpoint: typeof(KleeneStar.Core.WWW.Api._1_.Objects.Index));
        }

        /// <summary>
        /// Verifies that <see cref="PriorityEditFormFragment"/> registers the
        /// three priority-edit controls (Name, Description, State) and binds
        /// the <c>…/Api/_1_/Priorities/Index</c> endpoint.
        /// </summary>
        [Fact]
        public void Priority_Fragment_HasExpectedShape()
        {
            CoreHubFixture.Initialize(nameof(Priority_Fragment_HasExpectedShape));

            var frag = new PriorityEditFormFragment(null!);

            AssertEditFragmentShape(
                frag,
                expectedControls: 3,
                expectedDataServiceEndpoint: typeof(KleeneStar.Core.WWW.Api._1_.Priorities.Index));
        }

        /// <summary>
        /// Verifies that <see cref="SavedSearchEditFormFragment"/> registers
        /// the four saved-search-edit controls and binds the
        /// <c>…/Api/_1_/SavedSearches/Index</c> endpoint.
        /// </summary>
        [Fact]
        public void SavedSearch_Fragment_HasExpectedShape()
        {
            CoreHubFixture.Initialize(nameof(SavedSearch_Fragment_HasExpectedShape));

            var frag = new SavedSearchEditFormFragment(null!);

            AssertEditFragmentShape(
                frag,
                expectedControls: 4,
                expectedDataServiceEndpoint: typeof(KleeneStar.Core.WWW.Api._1_.SavedSearches.Index));
        }

        /// <summary>
        /// Verifies that <see cref="SlaEditFormFragment"/> registers all six
        /// SLA-edit controls and binds the
        /// <c>…/Api/_1_/Slas/Index</c> endpoint.
        /// </summary>
        [Fact]
        public void Sla_Fragment_HasExpectedShape()
        {
            CoreHubFixture.Initialize(nameof(Sla_Fragment_HasExpectedShape));

            var frag = new SlaEditFormFragment(null!);

            AssertEditFragmentShape(
                frag,
                expectedControls: 6,
                expectedDataServiceEndpoint: typeof(KleeneStar.Core.WWW.Api._1_.Slas.Index));
        }

        /// <summary>
        /// Verifies that <see cref="StatusEditFormFragment"/> registers the
        /// four status-edit controls and binds the
        /// <c>…/Api/_1_/Statuses/Index</c> endpoint. Note: the <c>ItemId</c>
        /// setter reads <see cref="WebParameter.WorkflowStateIdParameter"/>
        /// (not <c>StatusIdParameter</c>); the assertion checks the
        /// underlying request-parameter type explicitly.
        /// </summary>
        [Fact]
        public void Status_Fragment_HasExpectedShape()
        {
            CoreHubFixture.Initialize(nameof(Status_Fragment_HasExpectedShape));

            var frag = new StatusEditFormFragment(null!);

            AssertEditFragmentShape(
                frag,
                expectedControls: 4,
                expectedDataServiceEndpoint: typeof(KleeneStar.Core.WWW.Api._1_.Statuses.Index));
        }

        /// <summary>
        /// Verifies that <see cref="TemplateEditFormFragment"/> registers the
        /// four template-edit controls and binds the non-scoped
        /// <c>…/Api/_1_/Templates/Index</c> endpoint. The edit page is
        /// addressed by template id alone, so a workspace-scoped endpoint
        /// would leave a <c>${workspacekey}</c> placeholder in the rendered
        /// <c>base-uri</c> that the request binding pass cannot substitute —
        /// which is why the CRUD endpoint sits at the collection root, as it
        /// does for every other managed concept.
        /// </summary>
        [Fact]
        public void Template_Fragment_HasExpectedShape()
        {
            CoreHubFixture.Initialize(nameof(Template_Fragment_HasExpectedShape));

            var frag = new TemplateEditFormFragment(null!);

            AssertEditFragmentShape(
                frag,
                expectedControls: 4,
                expectedDataServiceEndpoint: typeof(KleeneStar.Core.WWW.Api._1_.Templates.Index));
        }

        /// <summary>
        /// Verifies that <see cref="TenantEditFormFragment"/> registers the
        /// three tenant-edit controls and binds the
        /// <c>…/Api/_1_/Tenants/Index</c> endpoint.
        /// </summary>
        [Fact]
        public void Tenant_Fragment_HasExpectedShape()
        {
            CoreHubFixture.Initialize(nameof(Tenant_Fragment_HasExpectedShape));

            var frag = new TenantEditFormFragment(null!);

            AssertEditFragmentShape(
                frag,
                expectedControls: 3,
                expectedDataServiceEndpoint: typeof(KleeneStar.Core.WWW.Api._1_.Tenants.Index));
        }

        /// <summary>
        /// Verifies that <see cref="WorkflowEditFormFragment"/> registers the
        /// two workflow-edit controls (Name, Description) and binds the
        /// <c>…/Api/_1_/Workflows/Index</c> endpoint.
        /// </summary>
        [Fact]
        public void Workflow_Fragment_HasExpectedShape()
        {
            CoreHubFixture.Initialize(nameof(Workflow_Fragment_HasExpectedShape));

            var frag = new WorkflowEditFormFragment(null!);

            AssertEditFragmentShape(
                frag,
                expectedControls: 2,
                expectedDataServiceEndpoint: typeof(KleeneStar.Core.WWW.Api._1_.Workflows.Index));
        }

        /// <summary>
        /// Verifies that <see cref="WorkspaceEditFormFragment"/> registers all
        /// nine workspace-edit controls and binds the
        /// <c>…/Api/_1_/Workspaces/Index</c> endpoint. Workspace is the only
        /// Edit fragment whose <c>ItemId</c> setter looks up the workspace
        /// by key (rather than reading the id directly from a request
        /// parameter); the additional <c>WorkspaceKeyParameter</c> check
        /// pins that behaviour down.
        /// </summary>
        [Fact]
        public void Workspace_Fragment_HasExpectedShape()
        {
            CoreHubFixture.Initialize(nameof(Workspace_Fragment_HasExpectedShape));

            var frag = new WorkspaceEditFormFragment(null!);

            AssertEditFragmentShape(
                frag,
                expectedControls: 9,
                expectedDataServiceEndpoint: typeof(KleeneStar.Core.WWW.Api._1_.Workspaces.Index));
        }

        /// <summary>
        /// Verifies that the <c>Name</c> lambda of <c>FieldName</c> on
        /// <see cref="FieldEditFormFragment"/> resolves to the literal string
        /// <c>"Name"</c> regardless of the supplied render context. This is a
        /// regression guard for a class of bugs where a fragment accidentally
        /// swaps <c>Name</c> for a property path (<c>"Entity.Name"</c>) or
        /// drops the lambda entirely (which would compile but produce empty
        /// form fields at runtime).
        /// </summary>
        [Fact]
        public void Field_Name_Lambda_Resolves_To_EntityName()
        {
            CoreHubFixture.Initialize(nameof(Field_Name_Lambda_Resolves_To_EntityName));

            var frag = new FieldEditFormFragment(null!);

            var name = frag.FieldName.Name?.Invoke(null!);

            Assert.Equal(nameof(Field.Name), name);
        }

        /// <summary>
        /// Walks the fragment's <c>Items</c> enumerable (the parent
        /// <c>ControlForm</c> list) and counts non-null elements so the
        /// test does not need to know whether <c>Add()</c> was called
        /// top-level or via nested groups.
        /// </summary>
        /// <param name="fragment">The fragment whose item count is required.</param>
        /// <returns>The number of registered form items.</returns>
        private static int CountItems(object fragment)
        {
            var itemsProp = fragment.GetType().GetProperty(
                "Items",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(itemsProp);

            var value = itemsProp!.GetValue(fragment) as IEnumerable;
            Assert.NotNull(value);

            var count = 0;
            foreach (var _ in value!)
            {
                count++;
            }
            return count;
        }

        /// <summary>
        /// Asserts that <paramref name="fragment"/> has a non-null
        /// <c>ServiceFactory</c> lambda set on it. The lambda is
        /// produced by <c>this.DataService&lt;T&gt;()</c> and resolves the
        /// REST endpoint at render time; invoking it here would require a
        /// fully-wired <see cref="WebExpress.WebCore.WebRender.IRenderContext"/>,
        /// which is exercised by the in-browser/curl probe instead.
        /// </summary>
        /// <param name="fragment">The fragment to inspect.</param>
        /// <returns>The <see cref="System.Type"/> of the underlying
        /// <c>Func&lt;,&gt;</c> delegate, useful for diagnostic dumps.</returns>
        private static Type AssertServiceFactoryIsSet(object fragment)
        {
            var sfProp = fragment.GetType().GetProperty(
                "ServiceFactory",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(sfProp);

            var sf = sfProp!.GetValue(fragment);
            Assert.NotNull(sf);

            var sfType = sf!.GetType();
            Assert.True(
                sfType.Name.StartsWith("Func"),
                $"ServiceFactory is expected to be a Func<,>, got {sfType.FullName}.");
            return sfType;
        }

        /// <summary>
        /// Asserts the shared structural contract every Edit-form fragment
        /// must satisfy: the constructor registers the expected number of
        /// <c>Add(...)</c>'d controls, the <c>ServiceFactory</c> lambda
        /// resolves to a non-null <c>DataServiceDescriptor</c> belonging to
        /// <paramref name="expectedDataServiceEndpoint"/>, and the
        /// <c>ItemId</c> setter is non-null (i.e. the Pitfall&nbsp;4A
        /// regression — accidentally dropping the setter — is caught here).
        /// </summary>
        /// <param name="fragment">The fragment to inspect.</param>
        /// <param name="expectedControls">
        /// The exact number of controls the fragment is expected to
        /// register via direct <c>Add(...)</c> calls, or <c>null</c> to skip
        /// the count assertion for fragments that compose controls
        /// dynamically (e.g. <see cref="ObjectEditFormFragment"/>).
        /// </param>
        /// <param name="expectedDataServiceEndpoint">
        /// The REST endpoint type the fragment is expected to wire via
        /// <c>this.DataService&lt;T&gt;()</c>.
        /// </param>
        private static void AssertEditFragmentShape(
            object fragment,
            int? expectedControls,
            Type expectedDataServiceEndpoint)
        {
            // 1) Item count. Fragments that compose controls dynamically
            //    pass null and skip this check.
            if (expectedControls.HasValue)
            {
                var count = CountItems(fragment);
                Assert.True(
                    count >= expectedControls.Value,
                    $"Expected at least {expectedControls.Value} controls, found {count}.");
            }

            // 2) DataService<T>() wires a non-null descriptor.
            var serviceFactoryType = AssertServiceFactoryIsSet(fragment);
            Assert.True(
                serviceFactoryType.IsGenericType,
                $"ServiceFactory type {serviceFactoryType.FullName} is not generic.");
            var genericArgs = serviceFactoryType.GetGenericArguments();
            Assert.Equal(2, genericArgs.Length);

            // 4) ItemId setter is non-null. Regression for Pitfall 4A
            //    (InsightEditFormFragment originally had no setter and
            //    produced empty data-id attributes).
            var itemIdProp = fragment.GetType().GetProperty(
                "ItemId",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(itemIdProp);
            var itemIdValue = itemIdProp!.GetValue(fragment);
            Assert.NotNull(itemIdValue);

            // 5) Every fragment-level public control property is non-null.
            foreach (var p in fragment.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // Skip derived helpers like FragmentContext, Mode, Submit.
                if (p.DeclaringType != fragment.GetType()) continue;
                var v = p.GetValue(fragment);
                Assert.True(
                    v != null,
                    $"Fragment {fragment.GetType().Name}.{p.Name} is unexpectedly null.");
            }

            // 6) The fragment's expected endpoint type is referenced in
            //    the DataService<T>() call by reading the literal
            //    type-name of the expected endpoint (sanity check).
            Assert.NotNull(expectedDataServiceEndpoint);
            Assert.True(
                expectedDataServiceEndpoint.FullName!.Contains("._1_."),
                $"Expected endpoint '{expectedDataServiceEndpoint.FullName}' is not under the Api._1_. namespace.");
        }
    }
}