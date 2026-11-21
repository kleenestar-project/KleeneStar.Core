using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

using TransferForm = KleeneStar.Core.WWW.Api._1_.Transfers.Form;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The wizard that moves an object with its subtree into another workspace
    /// (<see cref="ControlDataWizard"/> over <c>/api/1/transfers</c>, <c>kleenestar.hierarchy.md</c>,
    /// <i>Move Across Workspaces</i>).
    /// </summary>
    /// <remarks>
    /// Steps:
    /// <list type="number">
    ///   <item><c>workspace</c>: the target workspace as a tile selection with search, like the
    ///   classification dialog; the active workspaces the caller may read, without the object's own.</item>
    ///   <item><c>classes</c>, <c>review</c>: rendered by <see cref="TransferForm"/> from the
    ///   answers so far - the class mapping as tiles (keeping the class first) and what the move
    ///   does or why it is refused. Security levels without counterpart are named on the review
    ///   and removed by the move.</item>
    /// </list>
    /// The finish posts every answer; the endpoint answers the page of the moved object, which the
    /// wizard follows (<see cref="TypeRestFormSuccess.FollowUri"/>). Rendered for a signed-in caller
    /// who may change the object and has a workspace to move into.
    /// </remarks>
    [Title("kleenestar.core:transfer.dialog.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Transfer>]
    [Cache]
    public sealed class ObjectTransferFormFragment : FragmentControlDataWizard
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectTransferFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Transfers.Index>();

            Mode = _ => TypeRestFormMode.Add;
            SuccessAction = _ => TypeRestFormSuccess.FollowUri;
            FinishLabel = _ => "kleenestar.core:transfer.dialog.submit";
            FinishIcon = _ => new IconShare();
        }

        /// <summary>
        /// Renders the wizard for the object of the route.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Check(renderContext?.Request))
            {
                return null;
            }

            var request = renderContext.Request;
            var @object = CoreHub.ObjectManager.GetObjectByKey(request.GetParameter<ObjectKeyParameter>());

            if (@object is null
                || CoreHub.SessionManager.GetCurrentIdentityId(request) == Guid.Empty
                || !ContentAuthorization.MayWrite(@object, request))
            {
                return null;
            }

            var workspaces = ObjectTransferForm.TargetWorkspaces(@object);

            if (workspaces.Count == 0)
            {
                return new ControlAlert()
                {
                    Text = _ => "kleenestar.core:transfer.dialog.none",
                    BackgroundColor = _ => new PropertyColorBackgroundAlert(TypeColorBackgroundAlert.Info)
                }
                    .Render(renderContext, visualTree);
            }

            return Render(renderContext, visualTree, BuildPages(renderContext, @object, workspaces));
        }

        /// <summary>
        /// Builds the steps of the wizard.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="object">The moved object.</param>
        /// <param name="workspaces">The workspaces the object may be moved into.</param>
        /// <returns>The steps.</returns>
        private static IEnumerable<IControlDataWizardPage> BuildPages(IRenderControlContext renderContext, Model.Entities.Object @object, IReadOnlyList<Model.Entities.Workspace> workspaces)
        {
            var selection = new ControlFormItemInputTile
            {
                Name = _ => ObjectTransferForm.WorkspaceField,
                Label = _ => "kleenestar.core:transfer.dialog.workspace.label",
                Help = _ => "kleenestar.core:transfer.dialog.workspace.help",
                Searchable = _ => true,
                SearchPlaceholder = _ => "kleenestar.core:transfer.dialog.workspace.search",
                EmptyText = _ => "kleenestar.core:transfer.dialog.workspace.empty",
                Columns = _ => 2,
                Required = _ => true
            };

            foreach (var workspace in workspaces)
            {
                var category = workspace.Categories?.OrderBy(x => x.Name).FirstOrDefault()?.Name;
                var card = new ControlTileCard(workspace.Id.ToString())
                {
                    Header = _ => workspace.Name,
                    Icon = _ => workspace.Icon,
                    Badge = _ => category ?? workspace.Key,
                    BadgeColor = _ => new PropertyColorTile(CoreHub.AccentColor(workspace.Id))
                };

                if (!ProseText.IsEmpty(workspace.Description))
                {
                    card.Add(new ControlText { Text = _ => ProseText.ToPlainText(workspace.Description) });
                }

                card.AddFooter(new ControlText { Text = _ => workspace.Key });

                selection.Add(card);
            }

            var first = new ControlDataWizardPage("workspace")
            {
                Title = _ => "kleenestar.core:transfer.dialog.step.workspace.title",
                Subtitle = _ => I18N.Translate(renderContext, "kleenestar.core:transfer.dialog.step.workspace.subtitle",
                    string.IsNullOrWhiteSpace(@object.Summary) ? @object.Key : @object.Summary.Trim()),
                SummarySource = _ => ObjectTransferForm.WorkspaceField
            };

            first.Add
            (
                selection,
                new PresetHiddenInput { Name = _ => ObjectTransferForm.ObjectField, Value = _ => @object.Key },
                // one run per dialog: a repeated finish joins the run the first one started
                new PresetHiddenInput { Name = _ => ObjectTransferForm.KeyField, Value = _ => Guid.NewGuid().ToString("N") }
            );

            yield return first;

            static IUri Part(string part) => CoreHub.GetUri<TransferForm>()?.Add(new UriQuery("part", part));

            yield return new ControlDataWizardPage("classes")
            {
                Title = _ => "kleenestar.core:transfer.dialog.step.classes.title",
                Subtitle = _ => "kleenestar.core:transfer.dialog.step.classes.subtitle",
                Uri = _ => Part(TransferForm.PartClasses)
            };

            yield return new ControlDataWizardPage("review")
            {
                Title = _ => "kleenestar.core:transfer.dialog.step.review.title",
                Subtitle = _ => "kleenestar.core:transfer.dialog.step.review.subtitle",
                Uri = _ => Part(TransferForm.PartReview)
            };
        }
    }
}
