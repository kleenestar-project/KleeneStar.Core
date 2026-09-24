/**
 * KleeneStar: add-ons of the WYSIWYG editor.
 *
 * The framework's editor offers a registry of add-ons (webexpress.webui.EditorAddOns): a block or
 * inline element the author picks from the add-on library ({{ or the puzzle button), configures
 * through a property dialog and places in the text. The document stores only the add-on's name
 * and its properties; what it shows is drawn anew by its renderer in the editor and in the
 * reading view, and a controller registered for the rendered element (Controller.registerClass)
 * brings it to life - which is what lets an add-on here show live data of the installation.
 *
 * The add-ons are the ones a knowledge base and a service desk ask for:
 *
 *   ks-object    inline  an issue, asset or page by key - title and current state, linked
 *   ks-list      block   a live list of the objects a filter selects
 *   ks-count     block   the number of objects a filter selects, as a key figure
 *   ks-children  block   the pages directly below this page
 *   ks-news      block   the latest blog posts, of one workspace or all, as the blog overview shows them
 *   ks-toc       block   a table of contents built from the headings of the page
 *   ks-callout   block   a notice (info, tip, warning, danger) the author writes into
 *   ks-status    inline  a coloured status label
 *
 * The news are the blog overview's own feed (webexpress.webapp.FeedCtrl) over /api/1/blogs/feed or
 * /api/1/blogs/{workspacekey}/feed (meta kleenestar.editor.news), so a post looks the same in a
 * page as on the blog overview and the start page.
 *
 * The objects are read from /api/1/editor/objects, whose address the page names in
 * <meta name="kleenestar.editor.objects"> (EditorAddonMetaFragment). That endpoint reads through
 * the object manager, so every reader sees only what they may open: the same list in the same
 * document shows different objects to different readers, and a reference to a classified record
 * says "not found" instead of its title.
 *
 * The controller registry removes the class it adopted an element by, so every widget carries
 * two: ks-addon-* for the stylesheet and wx-kleenestar-addon-* for the registry. And the reading
 * view sets white-space: pre-wrap on the document, so the markup a controller writes carries no
 * whitespace between its elements - a line break there is a blank line on the page.
 *
 * The labels come from the kleenestar.core client translations (Assets/js/i18n), included ahead
 * of this file, because the registry reads them as plain strings when an add-on registers.
 */
(function () {
    const ui = window.webexpress && window.webexpress.webui;

    if (!ui || !ui.EditorAddOns || !ui.Controller || !ui.Ctrl) {
        return;
    }

    const PREFIX = "kleenestar.core:editor.addon.";
    const META_NAME = "kleenestar.editor.objects";
    const NEWS_META_NAME = "kleenestar.editor.news";
    const CACHE_TTL = 30000;

    /**
     * Translates a key of the add-ons.
     * @param {string} key The key below editor.addon.
     * @returns {string} The text.
     */
    const t = (key) => ui.I18N.translate(PREFIX + key);

    const CATEGORY = t("category");

    /**
     * Escapes a value for markup.
     * @param {any} value The value.
     * @returns {string} The escaped text.
     */
    const esc = (value) => String(value ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;" }[c]));

    /**
     * Returns the address of the objects endpoint, or null on a page that names none.
     * @returns {string|null} The address.
     */
    const endpoint = () => {
        const meta = document.querySelector(`meta[name="${META_NAME}"]`);
        const content = meta ? (meta.getAttribute("content") || "").trim() : "";

        return content.length > 0 ? content : null;
    };

    /**
     * The answers of the last few seconds, by address. The editor redraws an add-on whenever
     * the text around it changes; without this every keystroke would read the list again.
     */
    const cache = new Map();

    /**
     * Reads objects from the endpoint.
     * @param {object} params The query parameters; blank ones are left out.
     * @returns {Promise<object>} The answer ({ items, total, truncated }).
     */
    const query = async (params) => {
        const base = endpoint();

        if (!base) {
            throw new Error("no endpoint");
        }

        const search = new URLSearchParams();

        Object.entries(params || {}).forEach(([key, value]) => {
            if (value !== undefined && value !== null && String(value).trim() !== "") {
                search.set(key, String(value).trim());
            }
        });

        const uri = base + (base.includes("?") ? "&" : "?") + search.toString();
        const hit = cache.get(uri);

        if (hit && Date.now() - hit.time < CACHE_TTL) {
            return hit.promise;
        }

        const promise = fetch(uri, { headers: { "Accept": "application/json" }, credentials: "same-origin" })
            .then(async (response) => {
                const data = await response.json().catch(() => ({}));

                if (!response.ok) {
                    const error = new Error(data && data.error ? data.error : t("error"));
                    error.reason = data && data.error;
                    throw error;
                }

                return data;
            });

        cache.set(uri, { time: Date.now(), promise });
        promise.catch(() => cache.delete(uri));

        return promise;
    };

    /**
     * Builds the markup of a state chip.
     * @param {object|null} state The state of an object ({ label, tone }).
     * @returns {string} The chip, or an empty string.
     */
    const chip = (state) => state && state.label
        ? `<span class="ks-co-chip ks-co-tone-${esc(state.tone || "secondary")}">${esc(state.label)}</span>`
        : "";

    /**
     * Builds the markup of the icon of an object.
     * @param {object} item The object.
     * @returns {string} The icon.
     */
    const icon = (item) => item.icon
        ? `<img class="ks-addon-icon" src="${esc(item.icon)}" alt="">`
        : `<i class="ks-addon-icon ${esc(ui.IconSet.resolve("file"))}"></i>`;

    /**
     * Formats a date the way the reader's browser does.
     * @param {string} value An ISO date.
     * @returns {string} The date.
     */
    const date = (value) => {
        const parsed = value ? new Date(value) : null;

        return parsed && !isNaN(parsed) ? parsed.toLocaleDateString() : "";
    };

    /**
     * Returns the key of the object the page shows, for the add-ons that are about "this page".
     * The detail pages name its id (meta kleenestar.object); the edit routes only carry it in
     * the address.
     * @returns {string|null} The id or key.
     */
    const currentObject = () => {
        const meta = document.querySelector("meta[name=\"kleenestar.object\"]");
        const id = meta ? (meta.getAttribute("content") || "").trim() : "";

        if (id) {
            return id;
        }

        const match = window.location.pathname.match(/\/(?:document|blog|issue|asset)\/([^/]+)/i);

        return match ? decodeURIComponent(match[1]) : null;
    };

    /**
     * The filter properties the list and the figure share.
     * @returns {Array<object>} The property definitions.
     */
    const filterProperties = () => [
        { name: "workspace", label: t("filter.workspace"), type: "text", default: "" },
        { name: "class", label: t("filter.class"), type: "text", default: "" },
        {
            name: "kind", label: t("filter.kind"), type: "select", default: "issue",
            options: ["any", "issue", "asset", "document", "blog"].map((value) => ({ value, label: t("filter.kind." + value) }))
        },
        {
            name: "state", label: t("filter.state"), type: "select", default: "open",
            options: ["open", "done", "all"].map((value) => ({ value, label: t("filter.state." + value) }))
        },
        {
            name: "mine", label: t("filter.mine"), type: "select", default: "any",
            options: ["any", "me"].map((value) => ({ value, label: t("filter.mine." + value) }))
        },
        { name: "wql", label: t("filter.wql"), type: "text", default: "" }
    ];

    /**
     * Reads the filter a list or a figure carries on its element.
     * @param {HTMLElement} element The rendered add-on.
     * @returns {object} The query parameters.
     */
    const filterOf = (element) => ({
        workspace: element.dataset.workspace,
        class: element.dataset.class,
        kind: element.dataset.kind,
        state: element.dataset.state === "all" ? "" : element.dataset.state,
        mine: element.dataset.mine === "me" ? "1" : "",
        wql: element.dataset.wql
    });

    /**
     * Renders the filter settings onto the rendered element, so the controller finds them.
     * @param {object} data The escaped properties.
     * @returns {string} The data attributes.
     */
    const filterAttributes = (data) => ["workspace", "class", "kind", "state", "mine", "wql"]
        .map((name) => `data-${name}="${data[name] ?? ""}"`)
        .join(" ");

    // ------------------------------------------------------------------ registrations

    ui.EditorAddOns.register("ks-object", {
        label: t("object.label"),
        description: t("object.description"),
        icon: "link",
        type: "inline",
        category: CATEGORY,
        properties: [{ name: "key", label: t("object.key"), type: "text", default: "" }],
        renderer: (data) => `<span class="ks-addon-object wx-kleenestar-addon-object" data-key="${data.key || ""}">${data.key || "?"}</span>`
    });

    ui.EditorAddOns.register("ks-list", {
        label: t("list.label"),
        description: t("list.description"),
        icon: "list-check",
        type: "block",
        category: CATEGORY,
        properties: [...filterProperties(), { name: "max", label: t("filter.max"), type: "number", default: 10 }],
        renderer: (data) => `<div class="ks-addon-list wx-kleenestar-addon-list" ${filterAttributes(data)} data-max="${data.max || 10}"><span class="ks-addon-quiet">${esc(t("loading"))}</span></div>`
    });

    ui.EditorAddOns.register("ks-count", {
        label: t("count.label"),
        description: t("count.description"),
        icon: "chart-column",
        type: "block",
        category: CATEGORY,
        properties: [{ name: "caption", label: t("count.caption"), type: "text", default: t("list.label") }, ...filterProperties()],
        renderer: (data) => `<div class="ks-addon-count wx-kleenestar-addon-count" ${filterAttributes(data)}><span class="ks-addon-count-value">…</span><span class="ks-addon-count-caption">${data.caption || ""}</span></div>`
    });

    ui.EditorAddOns.register("ks-news", {
        label: t("news.label"),
        description: t("news.description"),
        icon: "newspaper",
        type: "block",
        category: CATEGORY,
        properties: [
            { name: "workspace", label: t("filter.workspace"), type: "text", default: "" },
            { name: "count", label: t("news.count"), type: "number", default: 3 }
        ],
        renderer: (data) => `<div class="ks-addon-news wx-kleenestar-addon-news" data-workspace="${data.workspace || ""}" data-count="${data.count || 3}"><span class="ks-addon-quiet">${esc(t("loading"))}</span></div>`
    });

    ui.EditorAddOns.register("ks-children", {
        label: t("children.label"),
        description: t("children.description"),
        icon: "sitemap",
        type: "block",
        category: CATEGORY,
        renderer: () => `<div class="ks-addon-children wx-kleenestar-addon-children"><span class="ks-addon-quiet">${esc(t("loading"))}</span></div>`
    });

    ui.EditorAddOns.register("ks-toc", {
        label: t("toc.label"),
        description: t("toc.description"),
        icon: "list-ol",
        type: "block",
        category: CATEGORY,
        renderer: () => `<nav class="ks-addon-toc wx-kleenestar-addon-toc" aria-label="${esc(t("toc.label"))}"></nav>`
    });

    ui.EditorAddOns.register("ks-callout", {
        label: t("callout.label"),
        description: t("callout.description"),
        icon: "lightbulb",
        type: "block",
        category: CATEGORY,
        isContainer: true,
        contentClass: "ks-addon-callout",
        properties: [
            {
                name: "variant", label: t("callout.variant"), type: "select", default: "info",
                options: ["info", "tip", "warning", "danger"].map((value) => ({ value, label: t("callout.variant." + value) }))
            },
            { name: "title", label: t("callout.title"), type: "text", default: "" }
        ],
        content: `<p>${esc(t("callout.content"))}</p>`
    });

    ui.EditorAddOns.register("ks-status", {
        label: t("status.label"),
        description: t("status.description"),
        icon: "tag",
        type: "inline",
        category: CATEGORY,
        properties: [
            { name: "text", label: t("status.text"), type: "text", default: t("status.text.default") },
            {
                name: "tone", label: t("status.tone"), type: "select", default: "primary",
                options: ["secondary", "primary", "success", "warning", "danger"].map((value) => ({ value, label: t("status.tone." + value) }))
            }
        ],
        // the tone is one of the listed options, and the renderer only ever sees escaped values
        renderer: (data) => `<span class="ks-addon-status ks-co-chip ks-co-tone-${/^[a-z]+$/.test(data.tone) ? data.tone : "secondary"}">${data.text || ""}</span>`
    });

    // ------------------------------------------------------------------ controllers

    /**
     * Renders a message in place of what could not be shown.
     * @param {HTMLElement} element The add-on.
     * @param {string} text The message.
     */
    const quiet = (element, text) => {
        element.innerHTML = `<span class="ks-addon-quiet">${esc(text)}</span>`;
    };

    /**
     * An inline reference to one object: icon, key, title as a link, state.
     */
    ui.KleeneStarObjectAddonCtrl = class extends ui.Ctrl {
        constructor(element) {
            super(element);
            this._load();
        }

        async _load() {
            const key = (this._element.dataset.key || "").trim();

            if (!key) {
                return;
            }

            try {
                const data = await query({ key });
                const item = data.items && data.items[0];

                if (!item) {
                    this._element.classList.add("ks-addon-missing");
                    this._element.title = t("notfound");
                    return;
                }

                this._element.innerHTML = `${icon(item)}<a href="${esc(item.uri)}" class="ks-addon-object-link"><span class="ks-addon-key">${esc(item.key)}</span> ${esc(item.summary)}</a>${chip(item.state)}`;
            } catch {
                this._element.title = t("error");
            }
        }
    };

    /**
     * A live list of the objects a filter selects.
     */
    ui.KleeneStarListAddonCtrl = class extends ui.Ctrl {
        constructor(element) {
            super(element);
            this._load();
        }

        async _load() {
            const max = parseInt(this._element.dataset.max, 10) || 10;

            try {
                const data = await query({ ...filterOf(this._element), max });
                const items = data.items || [];

                if (items.length === 0) {
                    quiet(this._element, t("empty"));
                    return;
                }

                const rows = items.map((item) => "<li class=\"ks-addon-row\">"
                    + icon(item)
                    + `<a href="${esc(item.uri)}" class="ks-addon-row-title"><span class="ks-addon-key">${esc(item.key)}</span> ${esc(item.summary)}</a>`
                    + `<span class="ks-addon-row-meta">${esc([item.workspace, date(item.updated)].filter(Boolean).join(" · "))}</span>`
                    + chip(item.state)
                    + "</li>").join("");

                const rest = (data.total || 0) - items.length;
                const more = rest > 0
                    ? `<div class="ks-addon-quiet">${esc(t("more").replace("{0}", (data.truncated ? "≥ " : "") + rest))}</div>`
                    : "";

                this._element.innerHTML = `<ul class="ks-addon-rows">${rows}</ul>${more}`;
            } catch (error) {
                quiet(this._element, error && error.reason ? error.reason : t("error"));
            }
        }
    };

    /**
     * The number of objects a filter selects.
     */
    ui.KleeneStarCountAddonCtrl = class extends ui.Ctrl {
        constructor(element) {
            super(element);
            this._load();
        }

        async _load() {
            const value = this._element.querySelector(".ks-addon-count-value");

            if (!value) {
                return;
            }

            try {
                const data = await query({ ...filterOf(this._element), max: 1 });

                value.textContent = (data.total || 0).toLocaleString();

                if (data.truncated) {
                    value.title = t("atleast");
                    value.textContent = "≥ " + value.textContent;
                }
            } catch (error) {
                value.textContent = "–";
                value.title = error && error.reason ? error.reason : t("error");
            }
        }
    };

    /**
     * The pages directly below the page the add-on stands on.
     */
    ui.KleeneStarChildrenAddonCtrl = class extends ui.Ctrl {
        constructor(element) {
            super(element);
            this._load();
        }

        async _load() {
            const parent = currentObject();

            if (!parent) {
                quiet(this._element, t("children.unsaved"));
                return;
            }

            try {
                const data = await query({ parent, max: 50 });
                const items = data.items || [];

                if (items.length === 0) {
                    quiet(this._element, t("children.none"));
                    return;
                }

                this._element.innerHTML = "<ul class=\"ks-addon-rows\">"
                    + items.map((item) => `<li class="ks-addon-row">${icon(item)}<a href="${esc(item.uri)}" class="ks-addon-row-title">${esc(item.summary)}</a></li>`).join("")
                    + "</ul>";
            } catch {
                quiet(this._element, t("error"));
            }
        }
    };

    /**
     * A table of contents from the headings of the document it stands in. In the reading view
     * the headings get anchors and the entries link to them; in the editor the entries are
     * plain text, because an id written into the working surface would end up in the document,
     * and the list follows the author's typing.
     */
    ui.KleeneStarTocAddonCtrl = class extends ui.Ctrl {
        constructor(element) {
            super(element);

            this._onInput = () => {
                window.clearTimeout(this._timer);
                this._timer = window.setTimeout(() => this.render(), 400);
            };

            // the reading view builds the document detached and inserts it in one piece, and
            // the registry adopts the element while it is still detached - the headings are
            // only there, and the document only found, once it has arrived on the page
            this._attach(0);
        }

        _attach(attempt) {
            if (!this._element.isConnected) {
                if (attempt < 50) {
                    this._timer = window.setTimeout(() => this._attach(attempt + 1), 100);
                }
                return;
            }

            // the registry strips the class a host was adopted by, so neither view is found
            // by its registration class: the editor keeps the add-on in its frame, the reading
            // view replaces the frame and marks its host "wx-content"
            const frame = this._element.closest(".wx-addon-frame");

            this._editing = !!frame;
            this._root = frame
                ? frame.parentElement?.closest("[contenteditable=\"true\"]") || frame.parentElement
                : this._element.closest(".wx-content") || this._element.closest(".wx-content-addon")?.parentElement;

            if (this._editing && this._root) {
                this._root.addEventListener("input", this._onInput);
            }

            this.render();
        }

        render() {
            const root = this._root;

            if (!root) {
                return;
            }

            const headings = Array.from(root.querySelectorAll("h1, h2, h3, h4"))
                .filter((heading) => !heading.closest(".ks-addon-toc, .wx-addon-frame .card-header") && heading.textContent.trim().length > 0);

            if (headings.length === 0) {
                quiet(this._element, t("toc.none"));
                return;
            }

            const top = Math.min(...headings.map((heading) => parseInt(heading.tagName.substring(1), 10)));
            const used = new Set();

            const entries = headings.map((heading) => {
                const level = parseInt(heading.tagName.substring(1), 10) - top;
                const text = heading.textContent.trim();

                if (this._editing) {
                    return `<li class="ks-addon-toc-level-${level}">${esc(text)}</li>`;
                }

                if (!heading.id) {
                    let slug = text.toLowerCase().replace(/[^\p{L}\p{N}]+/gu, "-").replace(/^-|-$/g, "") || "section";

                    while (used.has(slug) || document.getElementById(slug)) {
                        slug += "-";
                    }

                    heading.id = slug;
                }

                used.add(heading.id);

                return `<li class="ks-addon-toc-level-${level}"><a href="#${esc(heading.id)}">${esc(text)}</a></li>`;
            });

            this._element.innerHTML = `<div class="ks-addon-toc-title">${esc(t("toc.label"))}</div><ol>${entries.join("")}</ol>`;
        }

        destroy() {
            window.clearTimeout(this._timer);
            this._root?.removeEventListener("input", this._onInput);
        }
    };

    /**
     * The latest blog posts: the blog overview's feed control, handed its data service by hand.
     * A feed on a page gets its service from a wx-service island the server writes into the host;
     * the editor's sanitizer lets no such element into a widget, so the descriptor the island
     * would have carried is put where the service registry looks for parsed islands.
     */
    ui.KleeneStarNewsAddonCtrl = class extends ui.Ctrl {
        constructor(element) {
            super(element);

            const meta = document.querySelector(`meta[name="${NEWS_META_NAME}"]`);
            const base = meta ? (meta.getAttribute("content") || "").trim() : "";
            const Feed = window.webexpress?.webapp?.FeedCtrl;

            if (!base || !Feed) {
                quiet(element, t("error"));
                return;
            }

            // /api/1/blogs/feed reads every workspace, /api/1/blogs/{key}/feed one of them
            const workspace = (element.dataset.workspace || "").trim();
            const uri = workspace ? base.replace(/\/feed(\?.*)?$/, "/" + encodeURIComponent(workspace) + "/feed$1") : base;
            const count = Math.min(Math.max(parseInt(element.dataset.count, 10) || 3, 1), 20);

            const host = document.createElement("div");
            host._wxServiceDescriptors = [{ name: "data", kind: "rest", baseUri: uri, method: "GET" }];
            host.dataset.pageSize = String(count);
            host.dataset.moreLabel = t("news.more");
            host.dataset.emptyText = t("news.empty");
            host.dataset.openLabel = t("news.open");

            element.innerHTML = "";
            element.appendChild(host);

            this._feed = new Feed(host);
        }

        destroy() {
            this._feed?.destroy?.();
        }
    };

    ui.Controller.registerClass("wx-kleenestar-addon-object", ui.KleeneStarObjectAddonCtrl);
    ui.Controller.registerClass("wx-kleenestar-addon-list", ui.KleeneStarListAddonCtrl);
    ui.Controller.registerClass("wx-kleenestar-addon-count", ui.KleeneStarCountAddonCtrl);
    ui.Controller.registerClass("wx-kleenestar-addon-children", ui.KleeneStarChildrenAddonCtrl);
    ui.Controller.registerClass("wx-kleenestar-addon-toc", ui.KleeneStarTocAddonCtrl);
    ui.Controller.registerClass("wx-kleenestar-addon-news", ui.KleeneStarNewsAddonCtrl);
})();
