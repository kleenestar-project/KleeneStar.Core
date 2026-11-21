using System;
using WebExpress.WebApp.WebControl;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Builds the collaborative container an object's editing surfaces are rendered inside, so
    /// two people working on the same object see each other doing it: who is here, where their
    /// pointer is, where their caret sits, and what they type into a plain field.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The container id <b>is</b> the routing channel: the framework's control filters incoming
    /// messages by it, so everybody on the same object has to be given the same id and nobody
    /// else may be. That is why it is derived from the object id here rather than spelled out at
    /// each call site - a literal would drift, and a drifted id fails silently as "nobody else is
    /// here" rather than as an error.
    /// </para>
    /// <para>
    /// The transport is the message queue the application already runs; there is no server
    /// component to add. Peers reach each other when their sockets share a domain, which every
    /// object surface does through the data services it declares.
    /// </para>
    /// <para>
    /// The container holds the details sheet (<c>ObjectItemDetailFragment</c>): presence,
    /// pointers and carets, and the values of its plain inputs by value. The prose and
    /// spreadsheet editors are collaborative on their own: <c>ControlDataModalDocument</c> with
    /// <c>Collaborative</c>, <c>CollaborationId</c> and <c>Document</c> wraps the editor in a container of its own,
    /// and the framework edits a document of an editor host concurrently through operational
    /// transformation (<c>webexpress.webapp.CollaborativeDocument</c>). The shared draft
    /// (<see cref="Model.Entities.ObjectDraft"/>) stays the persistence of the edited text.
    /// </para>
    /// <para>
    /// <see cref="ControlCollaborative.Document"/> is the channel id, so the shared documents of
    /// an object are the same on every page that shows it; without it the framework scopes them
    /// by the path and query of the page.
    /// </para>
    /// </remarks>
    internal static class ObjectCollaborativeHost
    {
        /// <summary>
        /// Builds the collaborative container for the supplied object, populated with the
        /// supplied controls.
        /// </summary>
        /// <param name="object">The object whose surface is shared. Must not be null.</param>
        /// <param name="surface">A short token naming which surface of the object this is, so
        /// two surfaces of one object stay separate channels rather than echoing each other.</param>
        /// <param name="renderContext">The render context, read for the current identity.</param>
        /// <param name="controls">The controls rendered inside the container.</param>
        /// <returns>The configured container.</returns>
        public static ControlCollaborative Create
        (
            Model.Entities.Object @object,
            string surface,
            IRenderControlContext renderContext,
            params IControl[] controls
        )
        {
            ArgumentNullException.ThrowIfNull(@object);

            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext?.Request);
            var identity = identityId == Guid.Empty ? null : CoreHub.IdentityManager.GetIdentity(identityId);

            var channel = "collab-" + surface + "-" + @object.Id.ToString("N");

            return new ControlCollaborative(channel, controls)
            {
                // the documents belong to the record, not to the page: the detail page and the
                // preview pane of one object meet in the same documents
                Document = _ => channel,
                Classes = ["wx-kleenestar-collaborative"],
                Presence = _ => true,
                Cursor = _ => true,
                Input = _ => true,

                // an anonymous request still takes part - it is given no id here and the client
                // generates one per session, so a reader without an account shows up as a
                // visitor rather than as a second copy of whoever else is nameless
                UserId = _ => identityId == Guid.Empty ? null : identityId.ToString("N"),
                UserName = _ => identity?.Name
            };
        }
    }
}
