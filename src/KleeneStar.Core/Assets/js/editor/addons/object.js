var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * Registers ks-object as an editor add-on: an inline reference to an issue, asset or page by key -
 * icon, key, title as a link and the current state, read live for the reader.
 */
webexpress.webui.EditorAddOns.register("ks-object", {
    label: kleenestar.core.EditorAddons.t("object.label"),
    description: kleenestar.core.EditorAddons.t("object.description"),
    icon: "link",
    type: "inline",
    category: kleenestar.core.EditorAddons.category,
    properties: [{ name: "key", label: kleenestar.core.EditorAddons.t("object.key"), type: "text", default: "" }],
    renderer: (data) => `<span class="ks-addon-object wx-kleenestar-addon-object" data-key="${data.key || ""}">${data.key || "?"}</span>`
});

/**
 * The inline reference to one object.
 */
kleenestar.core.ObjectAddonCtrl = class extends webexpress.webui.Ctrl {
    /**
     * Constructor
     * @param {HTMLElement} element - The DOM element associated with the instance.
     */
    constructor(element) {
        super(element);

        this._load();
    }

    /**
     * Reads the object and draws the reference.
     */
    async _load() {
        const addons = kleenestar.core.EditorAddons;
        const key = (this._element.dataset.key || "").trim();

        if (!key) {
            return;
        }

        try {
            const data = await addons.query({ key });
            const item = data.items && data.items[0];

            if (!item) {
                this._element.classList.add("ks-addon-missing");
                this._element.title = addons.t("notfound");
                return;
            }

            this._element.innerHTML = `${addons.icon(item)}<a href="${addons.esc(item.uri)}" class="ks-addon-object-link"><span class="ks-addon-key">${addons.esc(item.key)}</span> ${addons.esc(item.summary)}</a>${addons.chip(item.state)}`;
        } catch {
            this._element.title = addons.t("error");
        }
    }
};

// register the class in the controller
webexpress.webui.Controller.registerClass("wx-kleenestar-addon-object", kleenestar.core.ObjectAddonCtrl);
