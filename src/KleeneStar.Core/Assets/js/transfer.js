var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * Moves an object (with its subtree) into another workspace (kleenestar.hierarchy.md, Move Across
 * Workspaces). The overflow menu of an object page links to the page with #ks-transfer
 * (ObjectTransferMoreFragment), which writes the operations endpoint of the object's workspace and
 * the object into <meta name="kleenestar.transfer">.
 *
 * The dialog picks the target workspace and shows the mapping the server plans - classes
 * (adjustable), dropped fields, how the states map, security levels without counterpart
 * (declassify) - then runs the transfer and opens the object under its new key. Only an answer to
 * the current choice is shown, and confirming sends exactly the request that plan was checked for.
 *
 * The page's elements belong to the framework's controls, so this is no control of its own but a
 * singleton listening on the document.
 */
kleenestar.core.Transfer = new class {
    /**
     * The prefix of the client texts.
     */
    PREFIX = "kleenestar.core:transfer.";

    /**
     * The meta element naming endpoint and object.
     */
    META_NAME = "kleenestar.transfer";

    /**
     * Constructor
     */
    constructor() {
        document.addEventListener("click", (e) => {
            const link = e.target && typeof e.target.closest === "function" ? e.target.closest("a[href$='#ks-transfer']") : null;
            if (link) {
                e.preventDefault();
                this.open();
            }
        });
    }

    /**
     * Translates a client text and fills {0}, {1}, ... placeholders.
     * @param {string} key - The key below kleenestar.core:transfer.
     * @param {...any} args - The values of the placeholders.
     * @returns {string} The text.
     */
    t(key, ...args) {
        let text = webexpress.webui.I18N.translate(this.PREFIX + key);
        if (!text || text === this.PREFIX + key) {
            text = key;
        }
        return args.reduce((s, a, i) => s.split("{" + i + "}").join(String(a)), text);
    }

    /**
     * Creates an element with attributes and children.
     * @param {string} tag - The tag name.
     * @param {Object} [attrs] - Attributes; `text` sets the text content, `on*` adds listeners.
     * @param {...(Node|string|null)} children - The children.
     * @returns {HTMLElement} The element.
     */
    el(tag, attrs, ...children) {
        const node = document.createElement(tag);
        for (const [name, value] of Object.entries(attrs || {})) {
            if (value === null || value === undefined || value === false) {
                continue;
            }
            if (name === "text") {
                node.textContent = value;
            } else if (name.startsWith("on") && typeof value === "function") {
                node.addEventListener(name.slice(2), value);
            } else if (name === "className") {
                node.className = value;
            } else {
                node.setAttribute(name, value === true ? "" : String(value));
            }
        }
        for (const child of children.flat()) {
            if (child !== null && child !== undefined) {
                node.append(child);
            }
        }
        return node;
    }

    /**
     * Calls an endpoint with JSON and answers status and body.
     * @param {string} method - The HTTP method.
     * @param {string} url - The address.
     * @param {Object} [body] - The JSON body.
     * @returns {Promise<{ok: boolean, status: number, data: any}>} The answer.
     */
    async api(method, url, body) {
        try {
            const response = await fetch(url, {
                method,
                credentials: "same-origin",
                headers: Object.assign({ "Accept": "application/json" }, body !== undefined ? { "Content-Type": "application/json; charset=utf-8" } : {}),
                body: body !== undefined ? JSON.stringify(body) : undefined
            });
            const text = await response.text();
            let data = null;
            try {
                data = text ? JSON.parse(text) : null;
            } catch {
                data = text;
            }
            return { ok: response.ok, status: response.status, data };
        } catch (error) {
            return { ok: false, status: 0, data: { message: String(error) } };
        }
    }

    /**
     * Reads the reason of a refused request.
     * @param {{data: any}} answer - The answer.
     * @returns {string} The reason.
     */
    reason(answer) {
        const data = answer && answer.data;
        if (data && typeof data === "object" && (data.message || data.error)) {
            return data.message || data.error;
        }
        return this.t("failed");
    }

    /**
     * Appends a query parameter to an address.
     * @param {string} url - The address.
     * @param {string} name - The parameter.
     * @param {string} value - The value.
     * @returns {string} The address.
     */
    withQuery(url, name, value) {
        return url + (url.includes("?") ? "&" : "?") + encodeURIComponent(name) + "=" + encodeURIComponent(value);
    }

    /**
     * Shows a short message at the foot of the page.
     * @param {string} message - The message.
     * @param {Object} [options] - `error` (flag), `duration` (ms), `stay` (keep until closed).
     * @returns {{set: Function, close: Function}} The handle.
     */
    toast(message, options = {}) {
        let host = document.querySelector(".ks-transfer-toasts");
        if (!host) {
            host = this.el("div", { className: "ks-transfer-toasts", role: "status", "aria-live": "polite" });
            document.body.append(host);
        }
        const text = this.el("span", { text: message });
        const item = this.el("div", { className: "ks-transfer-toast" + (options.error ? " error" : "") + (options.stay ? " progress" : "") }, text);
        const close = () => item.remove();
        if (!options.stay) {
            item.append(this.el("button", { type: "button", className: "close", "aria-label": this.t("close"), text: "×", onclick: close }));
            setTimeout(close, options.duration || 6000);
        }
        host.append(item);
        return { set: (value) => { text.textContent = value; }, close };
    }

    /**
     * Shows a dialog with the supplied body and answers what its OK callback returns.
     * @param {string} title - The title.
     * @param {HTMLElement} body - The body.
     * @param {Function} collect - Called on OK; answers the result or `undefined` to stay open.
     * @param {string} okText - The text of the OK button.
     * @returns {Promise<any>} The result, or `null` when cancelled.
     */
    dialog(title, body, collect, okText) {
        return new Promise((resolve) => {
            const box = this.el("dialog", { className: "ks-transfer-dialog", "aria-label": title });
            const form = this.el("form", { method: "dialog" });
            const done = (value) => {
                box.close();
                box.remove();
                resolve(value);
            };
            form.append(
                this.el("h2", { text: title }),
                body,
                this.el("div", { className: "ks-transfer-dialog-buttons" },
                    this.el("button", { type: "button", className: "secondary", text: this.t("cancel"), onclick: () => done(null) }),
                    this.el("button", { type: "submit", className: "primary", text: okText })));
            form.addEventListener("submit", (e) => {
                e.preventDefault();
                const value = collect();
                if (value !== undefined) {
                    done(value);
                }
            });
            box.addEventListener("cancel", (e) => {
                e.preventDefault();
                done(null);
            });
            box.append(form);
            document.body.append(box);
            box.showModal();
            const first = body.querySelector("select, input");
            if (first) {
                first.focus();
            }
        });
    }

    /**
     * Follows a started run until it ends, with a progress toast.
     * @param {string} base - The operations endpoint.
     * @param {Object} operation - The run as started.
     * @returns {Promise<Object>} The run as it ended.
     */
    async follow(base, operation) {
        const progress = this.toast(this.t("running", operation.done, operation.total), { stay: true });
        while (operation.state === "pending" || operation.state === "running") {
            await new Promise((r) => setTimeout(r, 1000));
            const answer = await this.api("GET", this.withQuery(base, "operation", operation.id));
            if (!answer.ok) {
                break;
            }
            operation = answer.data;
            progress.set(this.t("running", operation.done, operation.total));
        }
        progress.close();
        return operation;
    }

    /**
     * Opens the dialog for the object the page names.
     */
    async open() {
        const meta = document.querySelector(`meta[name="${this.META_NAME}"]`);
        let page;
        try {
            page = JSON.parse(meta ? meta.getAttribute("content") || "null" : "null");
        } catch {
            page = null;
        }
        if (!page || !page.endpoint || !page.object) {
            return;
        }
        const base = page.endpoint;
        const node = page.object;

        const first = await this.api("GET", this.withQuery(this.withQuery(base, "kind", "transfer"), "object", node));
        if (!first.ok) {
            this.toast(this.reason(first), { error: true });
            return;
        }
        const workspaces = first.data.workspaces || [];
        if (!workspaces.length) {
            this.toast(this.t("none"), { error: true });
            return;
        }

        // the request the shown plan answers is kept beside it: only an answer to the current
        // choice is shown, and confirming sends exactly the target the plan was checked for
        const state = { workspace: null, map: {}, declassify: false, plan: null, planned: null, sequence: 0 };
        const select = this.el("select", { className: "ks-transfer-input", "aria-label": this.t("workspace") },
            this.el("option", { value: "", text: this.t("choose") }),
            workspaces.map((w) => this.el("option", { value: w.key, text: w.name + " (" + w.key + ")" })));
        const mapping = this.el("div", { className: "ks-transfer-mapping", "aria-live": "polite" });
        const message = this.el("p", { className: "ks-transfer-warning", hidden: true });
        const total = this.el("p", { className: "ks-transfer-total", hidden: true });
        const body = this.el("div", { className: "ks-transfer" },
            this.el("label", { className: "ks-transfer-field" }, this.t("workspace"), select),
            mapping,
            message,
            total);

        const request = () => ({
            targetWorkspace: state.workspace,
            classMap: Object.assign({}, state.map),
            declassify: state.declassify
        });
        const sameRequest = (a, b) => !!a && !!b && JSON.stringify(a) === JSON.stringify(b);

        const load = async () => {
            const asked = request();
            const sequence = ++state.sequence;
            state.plan = null;
            state.planned = null;
            message.hidden = true;
            total.hidden = true;
            if (!state.workspace) {
                mapping.innerHTML = "";
                return;
            }
            mapping.setAttribute("aria-busy", "true");
            let url = this.withQuery(this.withQuery(this.withQuery(base, "kind", "transfer"), "object", node), "targetWorkspace", asked.targetWorkspace);
            const pairs = Object.entries(asked.classMap).map(([k, v]) => k + ":" + v);
            if (pairs.length) {
                url = this.withQuery(url, "map", pairs.join(","));
            }
            if (asked.declassify) {
                url = this.withQuery(url, "declassify", "true");
            }
            const answer = await this.api("GET", url);
            // a slower answer to an earlier choice must not replace the answer to the current one
            if (sequence !== state.sequence) {
                return;
            }
            mapping.removeAttribute("aria-busy");
            mapping.innerHTML = "";
            if (!answer.ok) {
                message.textContent = this.reason(answer);
                message.hidden = false;
                return;
            }
            const plan = answer.data;
            state.plan = plan;
            state.planned = asked;
            const transfer = plan.transfer || { classes: [], droppedFields: [], states: [], levels: [] };

            // classes, adjustable
            const classes = this.el("div", { className: "ks-transfer-section" }, this.el("h3", { text: this.t("classes") }));
            for (const c of transfer.classes) {
                const choice = this.el("select", { className: "ks-transfer-input", "aria-label": c.sourceName },
                    this.el("option", { value: "", text: this.t("unmapped") }),
                    (c.candidates || []).map((x) => this.el("option", { value: x.id, text: x.name, selected: x.id === c.targetId ? true : null })));
                choice.addEventListener("change", () => {
                    if (choice.value) {
                        state.map[c.sourceId] = choice.value;
                    } else {
                        delete state.map[c.sourceId];
                    }
                    load();
                });
                classes.append(this.el("label", { className: "ks-transfer-row" }, this.el("span", { text: c.sourceName + " →" }), choice));
            }
            mapping.append(classes);

            if (transfer.states.length) {
                mapping.append(this.el("div", { className: "ks-transfer-section" }, this.el("h3", { text: this.t("states") }),
                    this.el("ul", {}, transfer.states.map((x) => this.el("li", { text: x.className + ": " + x.from + " → " + (x.to || this.t("nostate")) })))));
            }
            if (transfer.droppedFields.length) {
                mapping.append(this.el("div", { className: "ks-transfer-section" }, this.el("h3", { text: this.t("dropped") }),
                    this.el("ul", {}, transfer.droppedFields.map((x) => this.el("li", { text: x.className + ": " + x.fieldName })))));
            }
            if (transfer.levels.some((x) => !x.targetId)) {
                const box = this.el("input", { type: "checkbox", checked: state.declassify ? true : null });
                box.addEventListener("change", () => {
                    state.declassify = box.checked;
                    load();
                });
                mapping.append(this.el("div", { className: "ks-transfer-section" }, this.el("h3", { text: this.t("levels") }),
                    this.el("ul", {}, transfer.levels.filter((x) => !x.targetId).map((x) => this.el("li", { text: x.name }))),
                    this.el("label", { className: "ks-transfer-choice" }, box, " ", this.t("declassify"))));
            }

            if (plan.refused) {
                message.textContent = plan.refused;
                message.hidden = false;
            } else {
                total.textContent = this.t("total", plan.total);
                total.hidden = false;
            }
        };

        select.addEventListener("change", () => {
            state.workspace = select.value || null;
            state.map = {};
            state.declassify = false;
            load();
        });

        // confirming takes only a plan that answers the current choice and was not refused; the
        // run is started with exactly the request that plan was checked for
        const confirmed = await this.dialog(this.t("title", page.label || node), body, () => {
            if (!state.plan || state.plan.refused || !sameRequest(state.planned, request())) {
                return undefined;
            }
            return state.planned;
        }, this.t("ok"));
        if (!confirmed) {
            return;
        }

        const started = await this.api("POST", base, Object.assign({ kind: "transfer", object: node }, confirmed));
        if (!started.ok) {
            this.toast(this.reason(started), { error: true });
            return;
        }
        const operation = await this.follow(base, started.data);
        if (operation.state === "failed") {
            this.toast(this.t("aborted", operation.done, operation.total, operation.error || this.t("failed")), { error: true, duration: 12000 });
            return;
        }
        this.toast(this.t("done", operation.done));
        setTimeout(() => {
            window.location.href = operation.result || window.location.href;
        }, 900);
    }
};
