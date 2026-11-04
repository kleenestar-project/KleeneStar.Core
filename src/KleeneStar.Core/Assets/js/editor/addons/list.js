var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * Registers ks-list as an editor add-on: a live list of the objects a filter selects.
 */
webexpress.webui.EditorAddOns.register("ks-list", {
    label: kleenestar.core.EditorAddons.t("list.label"),
    description: kleenestar.core.EditorAddons.t("list.description"),
    icon: "list-check",
    type: "block",
    category: kleenestar.core.EditorAddons.category,
    properties: [...kleenestar.core.EditorAddons.filterProperties(), { name: "max", label: kleenestar.core.EditorAddons.t("filter.max"), type: "number", default: 10 }],
    renderer: (data) => `<div class="ks-addon-list wx-kleenestar-addon-list" ${kleenestar.core.EditorAddons.filterAttributes(data)} data-max="${data.max || 10}"><span class="ks-addon-quiet">${kleenestar.core.EditorAddons.esc(kleenestar.core.EditorAddons.t("loading"))}</span></div>`
});

/**
 * The live list of the objects a filter selects.
 */
kleenestar.core.ListAddonCtrl = class extends webexpress.webui.Ctrl {
    /**
     * Constructor
     * @param {HTMLElement} element - The DOM element associated with the instance.
     */
    constructor(element) {
        super(element);

        this._load();
    }

    /**
     * Reads the objects and draws the list.
     */
    async _load() {
        const addons = kleenestar.core.EditorAddons;
        const max = parseInt(this._element.dataset.max, 10) || 10;

        try {
            const data = await addons.query({ ...addons.filterOf(this._element), max });
            const items = data.items || [];

            if (items.length === 0) {
                addons.quiet(this._element, addons.t("empty"));
                return;
            }

            const rows = items.map((item) => "<li class=\"ks-addon-row\">"
                + addons.icon(item)
                + `<a href="${addons.esc(item.uri)}" class="ks-addon-row-title"><span class="ks-addon-key">${addons.esc(item.key)}</span> ${addons.esc(item.summary)}</a>`
                + `<span class="ks-addon-row-meta">${addons.esc([item.workspace, addons.date(item.updated)].filter(Boolean).join(" · "))}</span>`
                + addons.chip(item.state)
                + "</li>").join("");

            const rest = (data.total || 0) - items.length;
            const more = rest > 0
                ? `<div class="ks-addon-quiet">${addons.esc(addons.t("more").replace("{0}", (data.truncated ? "≥ " : "") + rest))}</div>`
                : "";

            this._element.innerHTML = `<ul class="ks-addon-rows">${rows}</ul>${more}`;
        } catch (error) {
            addons.quiet(this._element, error && error.reason ? error.reason : addons.t("error"));
        }
    }
};

// register the class in the controller
webexpress.webui.Controller.registerClass("wx-kleenestar-addon-list", kleenestar.core.ListAddonCtrl);
