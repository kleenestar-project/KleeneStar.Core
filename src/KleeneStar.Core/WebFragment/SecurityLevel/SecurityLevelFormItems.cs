using KleeneStar.Core.WebControl;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebUI.WebControl;

using SecurityLevelEntity = KleeneStar.Model.Entities.SecurityLevel;

namespace KleeneStar.Core.WebFragment.SecurityLevel
{
    /// <summary>
    /// Builds the inputs a security level form is made of.
    /// </summary>
    /// <remarks>
    /// The add, edit and clone dialogs ask for the same thing - a security level has no half
    /// that only one of them edits - so the inputs are created here rather than written out
    /// three times. Each dialog owns its own instances; a control carries the value of the
    /// render it belongs to and cannot be shared between forms.
    /// </remarks>
    internal static class SecurityLevelFormItems
    {
        /// <summary>
        /// Creates the input for the name of the security level.
        /// </summary>
        /// <returns>The input control.</returns>
        public static ControlFormItemInputText CreateName()
        {
            return new ControlFormItemInputText()
            {
                Name = _ => nameof(SecurityLevelEntity.Name),
                Label = _ => "kleenestar.core:securitylevel.name.label",
                Placeholder = _ => "kleenestar.core:securitylevel.name.placeholder",
                Help = _ => "kleenestar.core:securitylevel.name.help",
                Required = _ => true
            };
        }

        /// <summary>
        /// Creates the input for the description of the security level.
        /// </summary>
        /// <returns>The input control.</returns>
        public static ControlFormItemInputText CreateDescription()
        {
            return new ControlFormItemInputText()
            {
                Name = _ => nameof(SecurityLevelEntity.Description),
                Label = _ => "kleenestar.core:securitylevel.description.label",
                Placeholder = _ => "kleenestar.core:securitylevel.description.placeholder",
                Format = _ => TypeEditTextFormat.Wysiwyg,
                LinkLibraryUri = CoreHub.EditorLinkLibrary,
                Required = _ => false
            };
        }

        /// <summary>
        /// Creates the multi-select naming the groups the level clears.
        /// </summary>
        /// <remarks>
        /// This is the whole feature in one input: what it names is who sees the objects the
        /// level is put on. Leaving it empty closes the level, which the help text says out
        /// loud rather than leaving to be discovered.
        /// </remarks>
        /// <returns>The input control.</returns>
        public static ControlDataFormItemInputSelection CreateClearance()
        {
            return new ControlDataFormItemInputSelection()
            {
                Name = _ => nameof(SecurityLevelEntity.PermittedGroupIds),
                Label = _ => "kleenestar.core:securitylevel.clearance.label",
                Placeholder = _ => "kleenestar.core:securitylevel.clearance.placeholder",
                Help = _ => "kleenestar.core:securitylevel.clearance.help",
                MultiSelect = _ => true,
                ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.SecurityLevels.Groups>().ToString())
            };
        }

        /// <summary>
        /// Creates the input for the rank of the security level.
        /// </summary>
        /// <returns>The input control.</returns>
        public static ControlFormItemInputText CreateRank()
        {
            return new ControlFormItemInputText()
            {
                Name = _ => nameof(SecurityLevelEntity.Rank),
                Label = _ => "kleenestar.core:securitylevel.rank.label",
                Placeholder = _ => "kleenestar.core:securitylevel.rank.placeholder",
                Help = _ => "kleenestar.core:securitylevel.rank.help",
                Format = _ => TypeEditTextFormat.Default,
                Required = _ => false
            };
        }

        /// <summary>
        /// Creates the switch marking the level as the one new objects start on.
        /// </summary>
        /// <returns>The input control.</returns>
        public static ControlFormItemInputCheck CreateIsDefault()
        {
            return new ControlFormItemInputCheck()
            {
                Name = _ => nameof(SecurityLevelEntity.IsDefault),
                Label = _ => "kleenestar.core:securitylevel.isdefault.label",
                Help = _ => "kleenestar.core:securitylevel.isdefault.help",
                Layout = _ => TypeLayoutCheck.Switch
            };
        }

        /// <summary>
        /// Creates the selection for the state of the security level.
        /// </summary>
        /// <returns>The input control.</returns>
        public static ControlDataFormItemInputSelection CreateState()
        {
            return new ControlDataFormItemInputSelection()
            {
                Name = _ => nameof(SecurityLevelEntity.State),
                Label = _ => "kleenestar.core:securitylevel.state.label",
                Placeholder = _ => "kleenestar.core:securitylevel.state.placeholder",
                Help = _ => "kleenestar.core:securitylevel.state.help",
                StickySelection = _ => true,
                ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.SecurityLevels.State>().ToString())
            };
        }
    }
}
