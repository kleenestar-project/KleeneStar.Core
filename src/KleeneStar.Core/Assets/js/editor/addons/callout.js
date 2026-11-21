var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * Registers ks-callout as an editor add-on: a notice (info, tip, warning, danger) the author writes
 * into. Existing notices keep their variant property, including the legacy tip value.
 */
webexpress.webui.EditorAddOns.register("ks-callout", {
    label: kleenestar.core.EditorAddons.t("callout.label"),
    description: kleenestar.core.EditorAddons.t("callout.description"),
    icon: "lightbulb",
    type: "block",
    category: kleenestar.core.EditorAddons.category,
    isContainer: true,
    // the framework puts the class on the editable body in the editor and on the reading
    // block in the reading view, so one rule styles both
    bodyClass: "ks-addon-callout",
    properties: [
        {
            name: "variant", label: kleenestar.core.EditorAddons.t("callout.variant"), type: "select", default: "info",
            options: ["info", "tip", "warning", "danger"].map((value) => ({ value, label: kleenestar.core.EditorAddons.t("callout.variant." + value) }))
        },
        { name: "title", label: kleenestar.core.EditorAddons.t("callout.title"), type: "text", default: "" }
    ],
    content: `<p>${kleenestar.core.EditorAddons.esc(kleenestar.core.EditorAddons.t("callout.content"))}</p>`
});
