using KleeneStar.Core.WebRestApi;
using System;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment
{
    /// <summary>
    /// Tells the header who is signed in: the name and the profile picture of the current
    /// identity, for the avatar in the application header.
    /// </summary>
    /// <remarks>
    /// The framework's header avatar (<c>ControlWebAppHeaderAvatar</c>) knows nothing of the
    /// application's identities. It asks the one <c>FragmentControlAvatar</c> scoped to
    /// <c>SectionAppAvatar</c> for a user name and a picture, and without such a fragment it
    /// draws its stock silhouette for everyone - which is why the picture set on the profile
    /// page never reached the header. This is that fragment; it answers from the session the
    /// way everything per-user does (<see cref="WebManager.ISessionManager.GetCurrentIdentityId"/>).
    /// <para>
    /// The picture is the identity's own only when it is a real one. Every identity carries
    /// <see cref="Model.Entities.Identity.Avatar"/>, because the generated mark is written at creation and
    /// restored when a picture is removed - but that mark is the product star tinted from the
    /// id, not a portrait, and it would put the same drawing on everybody's header. The boards
    /// tell the two apart by the file name (<see cref="ObjectBoardProjection.AvatarImage"/>)
    /// and fall back to initials; the header does the same, and the avatar control derives the
    /// initials from the name it is given. An anonymous caller keeps the framework's default.
    /// </para>
    /// <para>
    /// Each application needs one, because a fragment is registered per application: the core
    /// and the portal each derive a sealed fragment and scope it to their own pages.
    /// </para>
    /// </remarks>
    public abstract class SessionAvatarFragmentBase : FragmentControlAvatar
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        protected SessionAvatarFragmentBase(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Username = renderContext => ResolveIdentity(renderContext)?.Name;
            Image = renderContext => ResolvePicture(ResolveIdentity(renderContext));
        }

        /// <summary>
        /// Returns the picture of the signed-in identity.
        /// </summary>
        /// <remarks>
        /// The base answers the framework's silhouette wherever <see cref="WebExpress.WebUI.WebControl.ControlAvatar.Image"/>
        /// yields nothing, which is right for nobody and wrong for somebody without a portrait:
        /// the control shows initials only when it is handed no picture at all.
        /// </remarks>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <returns>The address of the picture; <see langword="null"/> for a signed-in identity
        /// without a portrait, so the control letters them; the framework's default for an
        /// anonymous caller.</returns>
        public override IUri GetImage(IRenderControlContext renderContext)
        {
            var identity = ResolveIdentity(renderContext);

            return identity is null
                ? base.GetImage(renderContext)
                : ResolvePicture(identity);
        }

        /// <summary>
        /// Resolves the identity the request is served for.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <returns>The identity, or <see langword="null"/> when nobody is signed in.</returns>
        private static Model.Entities.Identity ResolveIdentity(IRenderControlContext renderContext)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext?.Request);

            return identityId == Guid.Empty ? null : CoreHub.IdentityManager.GetIdentity(identityId);
        }

        /// <summary>
        /// Resolves the portrait of an identity, leaving the generated placeholder out.
        /// </summary>
        /// <param name="identity">The identity, may be null.</param>
        /// <returns>The address of the portrait, or <see langword="null"/>.</returns>
        private static IUri ResolvePicture(Model.Entities.Identity identity)
        {
            return ObjectBoardProjection.AvatarImage(identity) is null
                ? null
                : identity.Avatar?.Uri;
        }
    }
}
