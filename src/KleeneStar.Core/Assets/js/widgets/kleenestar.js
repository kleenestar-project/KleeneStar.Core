/**
 * Registers the KleeneStar-specific dashboard widgets with the client widget
 * registry. The framework ships the base registry, the widget "…" settings
 * dialog and the default widgets; this file only adds the app's own items.
 *
 * The widget strings live in the app's regular i18n (Internationalization/en|de,
 * namespace "kleenestar.core"); the dashboard fragment bridges them into the
 * client i18n registry ahead of this script, so the I18N lookups below resolve.
 */
webexpress.webui.DashboardWidgets.register("widget_kleenestar_note", {
    title: webexpress.webui.I18N.translate("kleenestar.core:dashboard.widget.note.title"),
    description: webexpress.webui.I18N.translate("kleenestar.core:dashboard.widget.note.description"),
    icon: "fas fa-note-sticky",

    settings: [
        {
            key: "text",
            label: webexpress.webui.I18N.translate("kleenestar.core:dashboard.widget.note.text"),
            type: "text",
            default: ""
        },
        {
            key: "tone",
            label: webexpress.webui.I18N.translate("kleenestar.core:dashboard.widget.note.tone"),
            type: "select",
            default: "secondary",
            options: [
                { value: "secondary", label: webexpress.webui.I18N.translate("kleenestar.core:dashboard.widget.note.tone.neutral") },
                { value: "primary", label: webexpress.webui.I18N.translate("kleenestar.core:dashboard.widget.note.tone.primary") },
                { value: "success", label: webexpress.webui.I18N.translate("kleenestar.core:dashboard.widget.note.tone.success") },
                { value: "warning", label: webexpress.webui.I18N.translate("kleenestar.core:dashboard.widget.note.tone.warning") },
                { value: "danger", label: webexpress.webui.I18N.translate("kleenestar.core:dashboard.widget.note.tone.danger") }
            ]
        }
    ],

    /**
     * Renders the note into the widget body as a toned alert.
     * @param {HTMLElement} container - The widget body element.
     * @param {object} data - The widget data, whose params carry the text and tone.
     */
    render: function (container, data) {
        const params = data.params || {};
        const tone = params.tone || "secondary";

        const note = document.createElement("div");
        note.className = "alert alert-" + tone + " mb-0";
        note.textContent = params.text || "";

        container.appendChild(note);
    }
});
