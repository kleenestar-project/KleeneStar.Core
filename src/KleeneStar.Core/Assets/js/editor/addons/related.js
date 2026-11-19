var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * Registers ks-related as an editor add-on: the pages that belong to the page it stands on, the
 * most relevant first (EditorRelatedPages): the pages it references or is related to, the pages
 * referencing it, its neighbours in the page tree and the pages sharing its labels.
 */
webexpress.webui.EditorAddOns.register("ks-related", {
    label: kleenestar.core.EditorAddons.t("related.label"),
    description: kleenestar.core.EditorAddons.t("related.description"),
    icon: "diagram-project",
    type: "block",
    category: kleenestar.core.EditorAddons.category,
    properties: [{ name: "max", label: kleenestar.core.EditorAddons.t("filter.max"), type: "number", default: 5 }],
    renderer: (data) => `<div class="ks-addon-related wx-kleenestar-addon-related" data-max="${data.max || 5}"><span class="ks-addon-quiet">${kleenestar.core.EditorAddons.esc(kleenestar.core.EditorAddons.t("loading"))}</span></div>`
});

/**
 * The pages that belong to the page the add-on stands on.
 */
kleenestar.core.RelatedAddonCtrl = class extends webexpress.webui.Ctrl {
    /**
     * Constructor
     * @param {HTMLElement} element - The DOM element associated with the instance.
     */
    constructor(element) {
        super(element);

        this._load();
    }

    /**
     * Reads the related pages and draws them under the title of the add-on.
     */
    async _load() {
        const addons = kleenestar.core.EditorAddons;
        const page = addons.currentObject();
        const max = Math.min(Math.max(parseInt(this._element.dataset.max, 10) || 5, 1), 50);
        const title = `<strong class="ks-addon-related-title">${addons.esc(addons.t("related.label"))}</strong>`;

        if (!page) {
            this._element.innerHTML = title + `<span class="ks-addon-quiet">${addons.esc(addons.t("related.unsaved"))}</span>`;
            return;
        }

        try {
            const data = await addons.query({ related: page, max });
            const items = data.items || [];

            if (items.length === 0) {
                this._element.innerHTML = title + `<span class="ks-addon-quiet">${addons.esc(addons.t("related.none"))}</span>`;
                return;
            }

            this._element.innerHTML = title + "<ul class=\"ks-addon-rows\">"
                + items.map((item) => `<li class="ks-addon-row">${addons.icon(item)}<a href="${addons.esc(item.uri)}" class="ks-addon-row-title">${addons.esc(item.summary)}</a></li>`).join("")
                + "</ul>";
        } catch {
            this._element.innerHTML = title + `<span class="ks-addon-quiet">${addons.esc(addons.t("error"))}</span>`;
        }
    }
};

// register the class in the controller
webexpress.webui.Controller.registerClass("wx-kleenestar-addon-related", kleenestar.core.RelatedAddonCtrl);
