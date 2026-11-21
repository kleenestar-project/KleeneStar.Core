using KleeneStar.Model.Entities;
using System.Reflection;
using WebExpress.WebCore.WebMessage;
using Banner = KleeneStar.Core.WWW.Api._1_.Workspaces.Banner;

namespace KleeneStar.Core.Test.WWW.Api.Workspaces
{
    /// <summary>
    /// Tests the banner a workspace's home page and opening post start with: drawn by
    /// <c>/api/1/workspaces/banner</c> since the reading view drops a <c>data:</c> image.
    /// The caption arrives in the address, so it must not reach the drawing as markup, and a
    /// request that names no workspace gets nothing to look at.
    /// </summary>
    public class UnitTestWorkspaceBanner
    {
        /// <summary>
        /// Draws a banner through the internal drawing of the template content.
        /// </summary>
        /// <param name="workspace">The workspace.</param>
        /// <param name="caption">The caption.</param>
        /// <returns>The SVG document.</returns>
        private static string Draw(Workspace workspace, string caption)
        {
            var type = typeof(CoreHub).Assembly.GetType("KleeneStar.Core.WebWorkspaceTemplate.WorkspaceTemplateContent");
            var method = type?.GetMethod("DrawBanner", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);

            return (string)method.Invoke(null, [workspace, caption]);
        }

        /// <summary>
        /// The caption and the key are text in the drawing, escaped, so an address carrying
        /// markup draws it as letters.
        /// </summary>
        [Fact]
        public void CaptionIsDrawnAsText()
        {
            var svg = Draw(new Workspace { Id = Guid.NewGuid(), Key = "A&B" }, "<script>alert(1)</script>");

            Assert.StartsWith("<svg ", svg);
            Assert.DoesNotContain("<script>", svg);
            Assert.Contains("&lt;script&gt;", svg);
            Assert.Contains(">A&amp;B<", svg);
        }

        /// <summary>
        /// A request naming no workspace, or one that is no id, is answered as not found.
        /// </summary>
        [Fact]
        public void NoWorkspaceIsNotFound()
        {
            Assert.IsType<ResponseNotFound>(new Banner().Get(null));
        }
    }
}
