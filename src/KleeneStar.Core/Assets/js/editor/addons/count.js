var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * Registers ks-count as an editor add-on: the number of objects a filter selects, as a key figure.
 */
webexpress.webui.EditorAddOns.register("ks-count", {
    label: kleenestar.core.EditorAddons.t("count.label"),
    description: kleenestar.core.EditorAddons.t("count.description"),
    icon: "chart-column",
    type: "block",
    category: kleenestar.core.EditorAddons.category,
    properties: [{ name: "caption", label: kleenestar.core.EditorAddons.t("count.caption"), type: "text", default: kleenestar.core.EditorAddons.t("list.label") }, ...kleenestar.core.EditorAddons.filterProperties()],
    renderer: (data) => `<div class="ks-addon-count wx-kleenestar-addon-count" ${kleenestar.core.EditorAddons.filterAttributes(data)}><span class="ks-addon-count-value">…</span><span class="ks-addon-count-caption">${data.caption || ""}</span></div>`
});

/**
 * The number of objects a filter selects.
 */
kleenestar.core.CountAddonCtrl = class extends webexpress.webui.Ctrl {
    /**
     * Constructor
     * @param {HTMLElement} element - The DOM element associated with the instance.
     */
    constructor(element) {
        super(element);

        this._load();
    }

    /**
     * Counts the objects and draws the figure.
     */
    async _load() {
        const addons = kleenestar.core.EditorAddons;
        const value = this._element.querySelector(".ks-addon-count-value");

        if (!value) {
            return;
        }

        try {
            const data = await addons.query({ ...addons.filterOf(this._element), max: 1 });

            value.textContent = (data.total || 0).toLocaleString();

            if (data.truncated) {
                value.title = addons.t("atleast");
                value.textContent = "≥ " + value.textContent;
            }
        } catch (error) {
            value.textContent = "–";
            value.title = error && error.reason ? error.reason : addons.t("error");
        }
    }
};

// register the class in the controller
webexpress.webui.Controller.registerClass("wx-kleenestar-addon-count", kleenestar.core.CountAddonCtrl);
