using KleeneStar.Core.WebManager;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Profile
{
    /// <summary>
    /// The list of devices and browsers signed in with the calling identity, each with the
    /// button that ends it, followed by the button that ends all of them at once.
    /// </summary>
    /// <remarks>
    /// The session the page is being served to carries a badge instead of a button: ending it
    /// from here would sign the user out of the very page they are reading.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Sessions.Index>]
    public sealed class ProfileSessionListFragment : FragmentControlPanel
    {
        private readonly IIdentitySessionManager _sessionManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        /// <param name="sessionManager">
        /// The manager used to read the sessions of the calling identity. Cannot be null.
        /// </param>
        public ProfileSessionListFragment(IFragmentContext fragmentContext, IIdentitySessionManager sessionManager)
            : base(fragmentContext)
        {
            _sessionManager = sessionManager;
        }

        /// <summary>
        /// Renders the session list. Returns <c>null</c> when the fragment's render conditions
        /// exclude it.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var sessions = _sessionManager.GetSessions(renderContext?.Request).ToList();

            var panel = new ControlPanel("profile-session-list");

            panel.Add(new ControlAlert("profile-session-hint")
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.sessions.hint"),
                BackgroundColor = _ => new PropertyColorBackgroundAlert(TypeColorBackgroundAlert.Info),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Two),
                Dismissibility = _ => TypeDismissibilityAlert.None
            });

            if (sessions.Count == 0)
            {
                panel.Add(new ControlText()
                {
                    Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.sessions.empty"),
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
                });

                return panel.Render(renderContext, visualTree);
            }

            var table = new ControlTable("profile-session-table")
            {
                Striped = _ => TypeStripedTable.Row,
                SuppressHeaders = _ => true
            }
                .AddColumn("")
                .AddColumn("")
                .AddColumn("");

            foreach (var session in sessions)
            {
                AddRow(table, renderContext, session);
            }

            panel.Add(table);

            if (sessions.Any(x => !x.Current))
            {
                panel.Add(new ControlButtonLink("profile-session-revokeall")
                {
                    Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.sessions.revokeall"),
                    Icon = _ => new IconArrowRightFromBracket(),
                    Outline = _ => true,
                    Uri = _ => Bind(CoreHub.GetUri<global::KleeneStar.Core.WWW.Profile.Sessions.RevokeAll>(), renderContext),
                    Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two, PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.None)
                });
            }

            return panel.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Adds one device row: the icon telling a handheld from a desktop, the device with its
        /// client, location, masked address and last activity, and either the badge marking the
        /// current device or the button that ends the session.
        /// </summary>
        /// <param name="table">The table the row is added to.</param>
        /// <param name="renderContext">The render context used for translating and binding.</param>
        /// <param name="session">The session the row describes.</param>
        private static void AddRow(IControlTable table, IRenderControlContext renderContext, IdentitySession session)
        {
            var device = new ControlPanel();

            device.Add(new ControlText()
            {
                Text = _ => session.Device,
                Format = _ => TypeFormatText.Bold
            });

            device.Add(new ControlText()
            {
                Text = _ => Describe(renderContext, session),
                Format = _ => TypeFormatText.Small,
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
            });

            var action = new ControlPanel();

            if (session.Current)
            {
                action.Add(new ControlBadge()
                {
                    Value = _ => I18N.Translate(renderContext, "kleenestar.core:profile.sessions.current"),
                    BackgroundColor = _ => new PropertyColorBackgroundBadge(TypeColorBackgroundBadge.Success)
                });
            }
            else
            {
                var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Profile.Sessions.Revoke>()
                    .Add(new UriQuery("id", session.Id.ToString()));

                action.Add(new ControlButtonLink()
                {
                    Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.sessions.revoke"),
                    Icon = _ => new IconArrowRightFromBracket(),
                    Outline = _ => true,
                    Size = _ => TypeSizeButton.Small,
                    Uri = _ => Bind(uri, renderContext)
                });
            }

            table.AddRow
            (
                new ControlTableCellPanel().Add(new ControlIcon()
                {
                    Icon = _ => session.Mobile ? new IconMobile() : new IconLaptop()
                }),
                new ControlTableCellPanel().Add(device),
                new ControlTableCellPanel().Add(action)
            );
        }

        /// <summary>
        /// Returns the line beneath the device name: the client, where the session was last
        /// seen, its masked address and how long ago it made a request.
        /// </summary>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <param name="session">The session being described.</param>
        /// <returns>The description line.</returns>
        private static string Describe(IRenderControlContext renderContext, IdentitySession session)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(session.Client))
            {
                parts.Add(session.Client);
            }

            if (!string.IsNullOrWhiteSpace(session.Location))
            {
                parts.Add(session.Location);
            }

            if (!string.IsNullOrWhiteSpace(session.IpAddress))
            {
                parts.Add(string.Format
                (
                    CultureInfo.CurrentCulture,
                    I18N.Translate(renderContext, "kleenestar.core:profile.sessions.ip"),
                    session.IpAddress
                ));
            }

            parts.Add(DescribeLastActive(renderContext, session));

            return string.Join(" · ", parts);
        }

        /// <summary>
        /// Returns how long ago the session last made a request, in the coarsest unit that
        /// still says something: now, hours, or days.
        /// </summary>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <param name="session">The session being described.</param>
        /// <returns>The last-activity phrase.</returns>
        private static string DescribeLastActive(IRenderControlContext renderContext, IdentitySession session)
        {
            var elapsed = DateTime.UtcNow - session.LastActive;

            if (elapsed < TimeSpan.FromMinutes(5))
            {
                return I18N.Translate(renderContext, "kleenestar.core:profile.sessions.lastactive.now");
            }

            if (elapsed < TimeSpan.FromDays(1))
            {
                return string.Format
                (
                    CultureInfo.CurrentCulture,
                    I18N.Translate(renderContext, "kleenestar.core:profile.sessions.lastactive.hours"),
                    Math.Max(1, (int)elapsed.TotalHours)
                );
            }

            return string.Format
            (
                CultureInfo.CurrentCulture,
                I18N.Translate(renderContext, "kleenestar.core:profile.sessions.lastactive.days"),
                Math.Max(1, (int)elapsed.TotalDays)
            );
        }

        /// <summary>
        /// Binds a target URI to the route parameters of the current request, so a link built
        /// here resolves the same way one built by the framework does.
        /// </summary>
        /// <param name="uri">The URI to bind.</param>
        /// <param name="renderContext">The render context carrying the request.</param>
        /// <returns>The bound URI.</returns>
        private static IUri Bind(IUri uri, IRenderControlContext renderContext)
        {
            return renderContext?.Request is null ? uri : uri.BindParameters(renderContext.Request);
        }
    }
}
