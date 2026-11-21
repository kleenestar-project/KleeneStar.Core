using KleeneStar.Core.WebParameter;
using System;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

using TemplateEntity = KleeneStar.Model.Entities.Template;
using WorkspaceEntity = KleeneStar.Model.Entities.Workspace;

namespace KleeneStar.Core.WebFragment.Template
{
    /// <summary>
    /// Represents the wizard a template is edited with (<see cref="TemplateWizardFragmentBase"/>),
    /// started from the stored record and its presets.
    /// </summary>
    /// <remarks>
    /// The classes may change: the presets are keyed by field name and the presets step shows
    /// the fields of the classes chosen now, so a preset never refers to a field the template no
    /// longer serves. The classes stay within the workspace of the template.
    /// </remarks>
    [Title("kleenestar.core:template.edit.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Template._templateid_.Edit>]
    [Cache]
    public sealed class TemplateEditFormFragment : TemplateWizardFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public TemplateEditFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Mode = _ => TypeRestFormMode.Edit;
            FinishLabel = _ => "kleenestar.core:template.wizard.finish.edit";
            FinishIcon = _ => new IconSave();
            ItemId = renderContext => ResolveTemplate(renderContext)?.Id.ToString();
        }

        /// <summary>
        /// Returns the template the route names.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <returns>The template, or null.</returns>
        protected override TemplateEntity ResolveTemplate(IRenderControlContext renderContext)
        {
            var value = renderContext?.Request?.GetParameter<TemplateIdParameter>()?.Value;

            return Guid.TryParse(value?.ToString(), out var id) ? CoreHub.TemplateManager.GetTemplate(id) : null;
        }

        /// <summary>
        /// Returns the workspace of the template's owning class.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="template">The edited template.</param>
        /// <returns>The workspace, or null.</returns>
        protected override WorkspaceEntity ResolveWorkspace(IRenderControlContext renderContext, TemplateEntity template)
        {
            var @class = template?.Class ?? (template is null ? null : CoreHub.ClassManager.GetClass(template.ClassId));

            return @class is null ? null : CoreHub.WorkspaceManager.GetWorkspace(@class.WorkspaceId);
        }
    }
}
