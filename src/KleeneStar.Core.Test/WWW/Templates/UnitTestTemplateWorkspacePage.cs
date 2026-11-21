using KleeneStar.Core.WWW.Templates._workspacekey_;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace KleeneStar.Core.Test.WWW.Templates
{
    /// <summary>
    /// Regression tests for the workspace-scoped template overview page.
    /// </summary>
    public class UnitTestTemplateWorkspacePage
    {
        /// <summary>
        /// Verifies that the template overview resolves its workspace from the
        /// workspace-key route rather than treating the key as a template/class id.
        /// </summary>
        [Fact]
        public void Process_UsesWorkspaceKeyParameter()
        {
            var sourcePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "KleeneStar.Core", "WWW", "Templates", "_workspacekey_", "Index.cs"));
            var source = File.ReadAllText(sourcePath);
            var processStart = source.IndexOf("public void Process", StringComparison.Ordinal);
            var processBody = source[processStart..];

            Assert.Contains("GetParameter<WorkspaceKeyParameter>()", processBody);
            Assert.Contains("GetWorkspaceByKey", processBody);
            Assert.DoesNotContain("GetParameter<TemplateIdParameter>()", processBody);
            Assert.DoesNotContain("ClassManager.GetClass", processBody);
        }

        /// <summary>
        /// Verifies that dependency injection supplies the manager actually used
        /// by the page.
        /// </summary>
        [Fact]
        public void Constructor_RequiresWorkspaceManager()
        {
            var constructor = typeof(global::KleeneStar.Core.WWW.Templates._workspacekey_.Index).GetConstructors().Single();
            var parameter = constructor.GetParameters().Single();

            Assert.Equal(typeof(global::KleeneStar.Core.WebManager.IWorkspaceManager), parameter.ParameterType);
        }
    }
}
