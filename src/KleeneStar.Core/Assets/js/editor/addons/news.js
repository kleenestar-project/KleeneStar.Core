var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * Registers ks-news as an editor add-on: the latest blog posts, of one workspace or all, as the
 * blog overview shows them. The posts are the blog overview's own feed (webexpress.webapp.FeedCtrl)
 * over /api/1/blogs/feed or /api/1/blogs/{workspacekey}/feed (meta kleenestar.editor.news), so a
 * post looks the same in a page as on the blog overview and the start page.
 */
webexpress.webui.EditorAddOns.register("ks-news", {
    label: kleenestar.core.EditorAddons.t("news.label"),
    description: kleenestar.core.EditorAddons.t("news.description"),
    icon: "newspaper",
    type: "block",
    category: kleenestar.core.EditorAddons.category,
    properties: [
        { name: "workspace", label: kleenestar.core.EditorAddons.t("filter.workspace"), type: "text", default: "" },
        { name: "count", label: kleenestar.core.EditorAddons.t("news.count"), type: "number", default: 3 }
    ],
    renderer: (data) => `<div class="ks-addon-news wx-kleenestar-addon-news" data-workspace="${data.workspace || ""}" data-count="${data.count || 3}"><span class="ks-addon-quiet">${kleenestar.core.EditorAddons.esc(kleenestar.core.EditorAddons.t("loading"))}</span></div>`
});

/**
 * The latest blog posts: the blog overview's feed control, handed its data service by hand.
 * A feed on a page gets its service from a wx-service island the server writes into the host;
 * the editor's sanitizer lets no such element into a widget, so the descriptor the island
 * would have carried is put where the service registry looks for parsed islands.
 */
kleenestar.core.NewsAddonCtrl = class extends webexpress.webui.Ctrl {
    /**
     * Constructor
     * @param {HTMLElement} element - The DOM element associated with the instance.
     */
    constructor(element) {
        super(element);

        const addons = kleenestar.core.EditorAddons;
        const meta = document.querySelector(`meta[name="${addons.NEWS_META_NAME}"]`);
        const base = meta ? (meta.getAttribute("content") || "").trim() : "";
        const Feed = window.webexpress?.webapp?.FeedCtrl;

        if (!base || !Feed) {
            addons.quiet(element, addons.t("error"));
            return;
        }

        // /api/1/blogs/feed reads every workspace, /api/1/blogs/{key}/feed one of them
        const workspace = (element.dataset.workspace || "").trim();
        const uri = workspace ? base.replace(/\/feed(\?.*)?$/, "/" + encodeURIComponent(workspace) + "/feed$1") : base;
        const count = Math.min(Math.max(parseInt(element.dataset.count, 10) || 3, 1), 20);

        const host = document.createElement("div");
        host._wxServiceDescriptors = [{ name: "data", kind: "rest", baseUri: uri, method: "GET" }];
        host.dataset.pageSize = String(count);
        host.dataset.moreLabel = addons.t("news.more");
        host.dataset.emptyText = addons.t("news.empty");
        host.dataset.openLabel = addons.t("news.open");

        element.innerHTML = "";
        element.appendChild(host);

        this._feed = new Feed(host);
    }

    /**
     * Destroys the control together with its feed.
     */
    destroy() {
        this._feed?.destroy?.();
    }
};

// register the class in the controller
webexpress.webui.Controller.registerClass("wx-kleenestar-addon-news", kleenestar.core.NewsAddonCtrl);
