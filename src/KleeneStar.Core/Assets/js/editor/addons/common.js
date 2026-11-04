var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * The shared helpers of KleeneStar's editor add-ons (kleenestar.editoraddons.md).
 *
 * The framework's editor offers a registry of add-ons (webexpress.webui.EditorAddOns): a block or
 * inline element the author picks from the add-on library ({{ or the puzzle button), configures
 * through a property dialog and places in the text. The document stores only the add-on's name
 * and its properties; what it shows is drawn anew by its renderer in the editor and in the
 * reading view, and a controller registered for the rendered element (Controller.registerClass)
 * brings it to life - which is what lets an add-on here show live data of the installation.
 *
 * One file per add-on beside this one, the way the framework's own add-ons are written
 * (editor/addons/*.js): object.js (ks-object), list.js (ks-list), count.js (ks-count),
 * news.js (ks-news), children.js (ks-children), legacy.js (ks-toc, ks-status) and callout.js
 * (ks-callout).
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
 * of these files, because the registry reads them as plain strings when an add-on registers.
 */
kleenestar.core.EditorAddons = new class {
    /**
     * The prefix of the add-ons' client texts.
     */
    PREFIX = "kleenestar.core:editor.addon.";

    /**
     * The meta element naming the objects endpoint.
     */
    META_NAME = "kleenestar.editor.objects";

    /**
     * The meta element naming the blog feed endpoint.
     */
    NEWS_META_NAME = "kleenestar.editor.news";

    /**
     * How long an answer of the objects endpoint is reused, in milliseconds.
     */
    CACHE_TTL = 30000;

    /**
     * The answers of the last few seconds, by address. The editor redraws an add-on whenever
     * the text around it changes; without this every keystroke would read the list again.
     */
    _cache = new Map();

    /**
     * Gets the category the add-ons are listed under in the library.
     * @returns {string} The category.
     */
    get category() {
        return this.t("category");
    }

    /**
     * Translates a key of the add-ons.
     * @param {string} key - The key below editor.addon.
     * @returns {string} The text.
     */
    t(key) {
        return webexpress.webui.I18N.translate(this.PREFIX + key);
    }

    /**
     * Escapes a value for markup.
     * @param {any} value - The value.
     * @returns {string} The escaped text.
     */
    esc(value) {
        return String(value ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;" }[c]));
    }

    /**
     * Returns the address of the objects endpoint, or null on a page that names none.
     * @returns {string|null} The address.
     */
    endpoint() {
        const meta = document.querySelector(`meta[name="${this.META_NAME}"]`);
        const content = meta ? (meta.getAttribute("content") || "").trim() : "";

        return content.length > 0 ? content : null;
    }

    /**
     * Reads objects from the endpoint.
     * @param {object} params - The query parameters; blank ones are left out.
     * @returns {Promise<object>} The answer ({ items, total, truncated }).
     */
    async query(params) {
        const base = this.endpoint();

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
        const hit = this._cache.get(uri);

        if (hit && Date.now() - hit.time < this.CACHE_TTL) {
            return hit.promise;
        }

        const promise = fetch(uri, { headers: { "Accept": "application/json" }, credentials: "same-origin" })
            .then(async (response) => {
                const data = await response.json().catch(() => ({}));

                if (!response.ok) {
                    const error = new Error(data && data.error ? data.error : this.t("error"));
                    error.reason = data && data.error;
                    throw error;
                }

                return data;
            });

        this._cache.set(uri, { time: Date.now(), promise });
        promise.catch(() => this._cache.delete(uri));

        return promise;
    }

    /**
     * Builds the markup of a state chip.
     * @param {object|null} state - The state of an object ({ label, tone }).
     * @returns {string} The chip, or an empty string.
     */
    chip(state) {
        return state && state.label
            ? `<span class="ks-co-chip ks-co-tone-${this.esc(state.tone || "secondary")}">${this.esc(state.label)}</span>`
            : "";
    }

    /**
     * Builds the markup of the icon of an object.
     * @param {object} item - The object.
     * @returns {string} The icon.
     */
    icon(item) {
        return item.icon
            ? `<img class="ks-addon-icon" src="${this.esc(item.icon)}" alt="">`
            : `<i class="ks-addon-icon ${this.esc(webexpress.webui.IconSet.resolve("file"))}"></i>`;
    }

    /**
     * Formats a date the way the reader's browser does.
     * @param {string} value - An ISO date.
     * @returns {string} The date.
     */
    date(value) {
        const parsed = value ? new Date(value) : null;

        return parsed && !isNaN(parsed) ? parsed.toLocaleDateString() : "";
    }

    /**
     * Returns the key of the object the page shows, for the add-ons that are about "this page".
     * The detail pages name its id (meta kleenestar.object); the edit routes only carry it in
     * the address.
     * @returns {string|null} The id or key.
     */
    currentObject() {
        const meta = document.querySelector("meta[name=\"kleenestar.object\"]");
        const id = meta ? (meta.getAttribute("content") || "").trim() : "";

        if (id) {
            return id;
        }

        const match = window.location.pathname.match(/\/(?:document|blog|issue|asset)\/([^/]+)/i);

        return match ? decodeURIComponent(match[1]) : null;
    }

    /**
     * The filter properties the list and the figure share.
     * @returns {Array<object>} The property definitions.
     */
    filterProperties() {
        return [
            { name: "workspace", label: this.t("filter.workspace"), type: "text", default: "" },
            { name: "class", label: this.t("filter.class"), type: "text", default: "" },
            {
                name: "kind", label: this.t("filter.kind"), type: "select", default: "issue",
                options: ["any", "issue", "asset", "document", "blog"].map((value) => ({ value, label: this.t("filter.kind." + value) }))
            },
            {
                name: "state", label: this.t("filter.state"), type: "select", default: "open",
                options: ["open", "done", "all"].map((value) => ({ value, label: this.t("filter.state." + value) }))
            },
            {
                name: "mine", label: this.t("filter.mine"), type: "select", default: "any",
                options: ["any", "me"].map((value) => ({ value, label: this.t("filter.mine." + value) }))
            },
            { name: "wql", label: this.t("filter.wql"), type: "text", default: "" }
        ];
    }

    /**
     * Reads the filter a list or a figure carries on its element.
     * @param {HTMLElement} element - The rendered add-on.
     * @returns {object} The query parameters.
     */
    filterOf(element) {
        return {
            workspace: element.dataset.workspace,
            class: element.dataset.class,
            kind: element.dataset.kind,
            state: element.dataset.state === "all" ? "" : element.dataset.state,
            mine: element.dataset.mine === "me" ? "1" : "",
            wql: element.dataset.wql
        };
    }

    /**
     * Renders the filter settings onto the rendered element, so the controller finds them.
     * @param {object} data - The escaped properties.
     * @returns {string} The data attributes.
     */
    filterAttributes(data) {
        return ["workspace", "class", "kind", "state", "mine", "wql"]
            .map((name) => `data-${name}="${data[name] ?? ""}"`)
            .join(" ");
    }

    /**
     * Renders a message in place of what could not be shown.
     * @param {HTMLElement} element - The add-on.
     * @param {string} text - The message.
     */
    quiet(element, text) {
        element.innerHTML = `<span class="ks-addon-quiet">${this.esc(text)}</span>`;
    }
};
