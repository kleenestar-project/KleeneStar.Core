using KleeneStar.Core.WebFragment.Landing;
using KleeneStar.Model.Entities;

namespace KleeneStar.Core.Test.WebFragment
{
    /// <summary>
    /// Tests which audit events the landing page counts as work - for the activity list and its
    /// key figure (<see cref="LandingActivitySection.IsWork"/>).
    /// </summary>
    public class UnitTestLandingRows
    {
        /// <summary>
        /// Only what a user did to an object in the content or workflow category is work - the
        /// installation starting and people signing in are not.
        /// </summary>
        [Theory]
        [InlineData(AuditOrigin.User, AuditCategory.Content, AuditTargetType.Object, true)]
        [InlineData(AuditOrigin.User, AuditCategory.Workflow, AuditTargetType.Object, true)]
        [InlineData(AuditOrigin.System, AuditCategory.Content, AuditTargetType.Object, false)]
        [InlineData(AuditOrigin.User, AuditCategory.Identity, AuditTargetType.Session, false)]
        [InlineData(AuditOrigin.System, AuditCategory.Lifecycle, AuditTargetType.Installation, false)]
        [InlineData(AuditOrigin.User, AuditCategory.Content, AuditTargetType.Workspace, false)]
        public void OnlyUserWorkOnObjectsCounts(AuditOrigin origin, AuditCategory category, AuditTargetType target, bool expected)
        {
            var @event = new AuditEvent { Origin = origin, Category = category, TargetType = target };

            Assert.Equal(expected, LandingActivitySection.IsWork(@event));
        }
    }
}
