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
    /// The list of personal access tokens of the calling identity: what each one is called,
    /// what it may do, when it was last used and when it runs out, plus the buttons that edit,
    /// revoke and delete it.
    /// </summary>
    /// <remarks>
    /// A token still in use is revoked rather than deleted, so the record of what existed and
    /// what it was allowed to do survives; deleting is offered once it is revoked or expired.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Tokens.Index>]
    public sealed class ProfileTokenListFragment : FragmentControlPanel
    {
        private readonly IAccessTokenManager _accessTokenManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        /// <param name="accessTokenManager">
        /// The manager used to read the tokens of the calling identity. Cannot be null.
        /// </param>
        public ProfileTokenListFragment(IFragmentContext fragmentContext, IAccessTokenManager accessTokenManager)
            : base(fragmentContext)
        {
            _accessTokenManager = accessTokenManager;
        }

        /// <summary>
        /// Renders the token list. Returns <c>null</c> when the fragment's render conditions
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

            var tokens = _accessTokenManager.GetAccessTokens(renderContext?.Request).ToList();

            var panel = new ControlPanel("profile-token-list");

            panel.Add(new ControlText()
            {
                Text = _ => string.Format
                (
                    CultureInfo.CurrentCulture,
                    I18N.Translate(renderContext, "kleenestar.core:profile.tokens.count"),
                    tokens.Count,
                    tokens.Count(x => x.State.IsActive())
                ),
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Two)
            });

            if (tokens.Count == 0)
            {
                panel.Add(new ControlText()
                {
                    Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.tokens.empty"),
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
                });
            }
            else
            {
                var table = new ControlTable("profile-token-table")
                {
                    Striped = _ => TypeStripedTable.Row,
                    SuppressHeaders = _ => true
                }
                    .AddColumn("")
                    .AddColumn("");

                foreach (var token in tokens)
                {
                    AddRow(table, renderContext, token);
                }

                panel.Add(table);
            }

            panel.Add(new ControlText()
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.tokens.hint"),
                Format = _ => TypeFormatText.Small,
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two, PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.None)
            });

            return panel.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Adds one token row: the label with its state badge, the prefix and dates, the scopes
        /// it grants, and the buttons that act on it.
        /// </summary>
        /// <param name="table">The table the row is added to.</param>
        /// <param name="renderContext">The render context used for translating and binding.</param>
        /// <param name="token">The token the row describes.</param>
        private static void AddRow(IControlTable table, IRenderControlContext renderContext, AccessToken token)
        {
            var details = new ControlPanel();

            details.Add(new ControlPanel
            (
                null,
                new ControlText()
                {
                    Text = _ => token.Name,
                    Format = _ => TypeFormatText.Bold
                },
                new ControlBadge()
                {
                    Value = _ => I18N.Translate(renderContext, token.State.Text()),
                    Classes = [token.State.Color()]
                }
            )
            {
                Styles = ["display: flex; gap: 0.5em; align-items: baseline;"]
            });

            details.Add(new ControlText()
            {
                Text = _ => Describe(renderContext, token),
                Format = _ => TypeFormatText.Small,
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
            });

            var scopes = new ControlPanel()
            {
                Styles = ["display: flex; gap: 0.35em; flex-wrap: wrap;"]
            };

            foreach (var scope in AccessTokenScope.Split(token.Scopes))
            {
                scopes.Add(new ControlBadge()
                {
                    Value = _ => scope,
                    BackgroundColor = _ => new PropertyColorBackgroundBadge(TypeColorBackgroundBadge.Light)
                });
            }

            details.Add(scopes);

            var actions = new ControlPanel()
            {
                Styles = ["display: flex; gap: 0.35em;"]
            };

            actions.Add(new ControlButtonLink()
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.tokens.edit"),
                Icon = _ => new IconPen(),
                Outline = _ => true,
                Size = _ => TypeSizeButton.Small,
                PrimaryAction = _ => new ActionModal
                (
                    "modal-form",
                    Bind(CoreHub.GetUri<global::KleeneStar.Core.WWW.Profile.Tokens.Edit>()
                        .Add(new UriQuery("id", token.Id.ToString())), renderContext),
                    TypeModalSize.Large
                )
            });

            if (token.State.IsActive())
            {
                actions.Add(new ControlButtonLink()
                {
                    Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.tokens.revoke"),
                    Icon = _ => new IconBan(),
                    Outline = _ => true,
                    Size = _ => TypeSizeButton.Small,
                    TextColor = _ => new PropertyColorText(TypeColorText.Danger),
                    Uri = _ => Bind(CoreHub.GetUri<global::KleeneStar.Core.WWW.Profile.Tokens.Revoke>()
                        .Add(new UriQuery("id", token.Id.ToString())), renderContext)
                });
            }
            else
            {
                actions.Add(new ControlButtonLink()
                {
                    Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.tokens.delete"),
                    Icon = _ => new IconTrashCan(),
                    Outline = _ => true,
                    Size = _ => TypeSizeButton.Small,
                    TextColor = _ => new PropertyColorText(TypeColorText.Danger),
                    Uri = _ => Bind(CoreHub.GetUri<global::KleeneStar.Core.WWW.Profile.Tokens.Delete>()
                        .Add(new UriQuery("id", token.Id.ToString())), renderContext)
                });
            }

            table.AddRow
            (
                new ControlTableCellPanel().Add(details),
                new ControlTableCellPanel().Add(actions)
            );
        }

        /// <summary>
        /// Returns the line beneath the token name: its prefix, when it was created, when it
        /// was last used and when it runs out.
        /// </summary>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <param name="token">The token being described.</param>
        /// <returns>The description line.</returns>
        private static string Describe(IRenderControlContext renderContext, AccessToken token)
        {
            var parts = new List<string>
            {
                token.Prefix + "…",
                string.Format
                (
                    CultureInfo.CurrentCulture,
                    I18N.Translate(renderContext, "kleenestar.core:profile.tokens.created"),
                    token.Created.ToLocalTime().ToString("d", CultureInfo.CurrentCulture)
                )
            };

            parts.Add(token.LastUsed.HasValue
                ? string.Format
                (
                    CultureInfo.CurrentCulture,
                    I18N.Translate(renderContext, "kleenestar.core:profile.tokens.lastused"),
                    token.LastUsed.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
                )
                : I18N.Translate(renderContext, "kleenestar.core:profile.tokens.neverused"));

            if (token.Expires.HasValue)
            {
                parts.Add(string.Format
                (
                    CultureInfo.CurrentCulture,
                    I18N.Translate(renderContext, "kleenestar.core:profile.tokens.expires"),
                    token.Expires.Value.ToLocalTime().ToString("d", CultureInfo.CurrentCulture)
                ));
            }

            return string.Join(" · ", parts);
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
