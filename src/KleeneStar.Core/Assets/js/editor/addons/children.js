var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * Registers ks-children as an editor add-on: the pages directly below the page it stands on.
 */
webexpress.webui.EditorAddOns.register("ks-children", {
    label: kleenestar.core.EditorAddons.t("children.label"),
    description: kleenestar.core.EditorAddons.t("children.description"),
    icon: "sitemap",
    type: "block",
    category: kleenestar.core.EditorAddons.category,
    renderer: () => `<div class="ks-addon-children wx-kleenestar-addon-children"><span class="ks-addon-quiet">${kleenestar.core.EditorAddons.esc(kleenestar.core.EditorAddons.t("loading"))}</span></div>`
});

/**
 * The pages directly below the page the add-on stands on.
 */
kleenestar.core.ChildrenAddonCtrl = class extends webexpress.webui.Ctrl {
    /**
     * Constructor
     * @param {HTMLElement} element - The DOM element associated with the instance.
     */
    constructor(element) {
        super(element);

        this._load();
    }

    /**
     * Reads the child pages and draws them.
     */
    async _load() {
        const addons = kleenestar.core.EditorAddons;
        const parent = addons.currentObject();

        if (!parent) {
            addons.quiet(this._element, addons.t("children.unsaved"));
            return;
        }

        try {
            const data = await addons.query({ parent, max: 50 });
            const items = data.items || [];

            if (items.length === 0) {
                addons.quiet(this._element, addons.t("children.none"));
                return;
            }

            this._element.innerHTML = "<ul class=\"ks-addon-rows\">"
                + items.map((item) => `<li class="ks-addon-row">${addons.icon(item)}<a href="${addons.esc(item.uri)}" class="ks-addon-row-title">${addons.esc(item.summary)}</a></li>`).join("")
                + "</ul>";
        } catch {
            addons.quiet(this._element, addons.t("error"));
        }
    }
};

// register the class in the controller
webexpress.webui.Controller.registerClass("wx-kleenestar-addon-children", kleenestar.core.ChildrenAddonCtrl);
