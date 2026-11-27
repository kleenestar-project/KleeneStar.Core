var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * Brings an object detail page up to date after its record changed.
 *
 * The detail page of an issue, an asset, a document or a blog post is rendered by the server
 * once, and the dialogs that write the record - the structured edit mask, the prose editor,
 * the security-level and organize dialogs - only close when they are done. Nothing re-rendered
 * the description, the property cards or the relation card afterwards, so the page went on
 * showing the record as it was when it was opened. A successful submit of a form that edits
 * that very record - the form's data-id is the object's id, so a dialog that writes something
 * else on the page, a comment or a relation of another object, is left alone - therefore
 * reloads the page once the form has told everybody else. A delete is not followed, because
 * the page would only reload into a "not found"; and a form that is not in a dialog is not
 * followed either, because a page-level form reports its own outcome in place.
 *
 * A workflow transition does not reload the page. The entries of the status menu are links to
 * the transition endpoint, which moves the object and redirects back here; a plain click was a
 * navigation, so the whole page was built again for a change of one badge. The click is taken
 * over instead: the link is requested in the background, and the page the redirect ends on -
 * this page, rendered with the new state - is laid over the one on screen element by element
 * (refresh). The transition dialog (a transition the workflow gives a form) ends the same way.
 *
 * The overlay compares server markup with server markup. The page as the server sent it is
 * kept from the moment the script runs - a deferred script, which the controller lets run
 * before it builds any control (it waits for DOMContentLoaded) - and after every refresh the
 * page just fetched takes its place. An element is replaced only where the two differ, and
 * only as a whole where a control owns it, so the selected tab, a half-written comment, the
 * scroll position and every section that did not change stay as they are.
 */
kleenestar.core.ObjectRefresh = new class {
    /**
     * The meta tag the detail page names its object in.
     */
    META_NAME = "kleenestar.object";

    /**
     * The meta tag the detail page names the transition endpoint of its object in.
     */
    TRANSITION_META_NAME = "kleenestar.object.transition";

    /**
     * The hidden input the transition dialog carries the target state in.
     */
    TRANSITION_STATE_INPUT = "TransitionStateId";

    /**
     * The regions of the page that show the record. Everything outside - the navigation, the
     * sidebar, the dialogs - is about the application, not about the object.
     */
    REGIONS = ["wx-breadcrumb", "wx-header-notifications", "wx-content"];

    /**
     * The controls that only frame their children and leave them in place, so a change inside
     * them is applied to the child that changed. Every other control owns the markup it was
     * built from and is replaced as a whole.
     */
    CONTAINERS = ["wx-webui-split", "wx-webui-section"];

    /**
     * Constructor
     */
    constructor() {
        // the server renders the page hidden until its controls are built; while the mark is
        // there, the page is still the markup the server sent. a page whose controls are built
        // already has no usable copy, and the first refresh fetches one first
        this._snapshot = document.documentElement.hasAttribute("data-wx-pending")
            ? this.capture(document, (element) => element.cloneNode(true))
            : null;
        this._queue = Promise.resolve();
        this._running = false;

        // captured, so the link is taken over before anything else reacts to the click
        document.addEventListener("click", (event) => this.onClick(event), true);

        document.addEventListener(webexpress.webui.Event.UPLOAD_SUCCESS_EVENT, (event) => {
            if (!this.changesThisObject(event)) {
                return;
            }

            // the transition dialog ends like the status menu: the page is laid over, not reloaded
            if (this.isTransitionForm(event.detail.form)) {
                this.enqueue(() => this.refresh());
                return;
            }

            // the reload is deferred a tick so the form's own listeners - the dialog closing,
            // the toast, the editor ending its draft - run against the page they were written for
            window.setTimeout(() => window.location.reload(), 0);
        });
    }

    /**
     * Reads the id of the object the page shows.
     * @param {Document} [doc=document] - The document to read.
     * @returns {string|null} The id, or null on a page that names none.
     */
    objectId(doc = document) {
        const meta = doc.querySelector(`meta[name="${this.META_NAME}"]`);
        const content = meta ? (meta.getAttribute("content") || "").trim().toLowerCase() : "";

        return content.length > 0 ? content : null;
    }

    /**
     * Reads the path of the transition endpoint of the object the page shows.
     * @returns {string|null} The path in lower case, or null on a page that names none.
     */
    transitionPath() {
        const meta = document.querySelector(`meta[name="${this.TRANSITION_META_NAME}"]`);
        const content = meta ? (meta.getAttribute("content") || "").trim() : "";

        if (content.length === 0) {
            return null;
        }

        try {
            return new URL(content, window.location.href).pathname.toLowerCase();
        } catch (_) {
            return null;
        }
    }

    /**
     * Decides whether a successful submit changed the object the page shows.
     * @param {CustomEvent} event - The upload success event of a form.
     * @returns {boolean} True when the page has to be brought up to date.
     */
    changesThisObject(event) {
        const form = event?.detail?.form;
        const id = this.objectId();

        if (!form || !id || !form.closest) {
            return false;
        }

        // a create or clone answers the new object's page, which its wizard follows (success
        // action follow-uri); a reload here would race that navigation
        if (event?.detail?.response?.data?.created === true) {
            return false;
        }

        // a form that is not in a dialog reports its outcome where it stands. The nesting
        // runs both ways: the modal form controller puts the served form inside its dialog,
        // the editor dialog puts its dialog inside the form
        if (!form.closest(".modal, .wx-webui-modal") && !form.querySelector(".modal, .wx-webui-modal")) {
            return false;
        }

        // the id survives on the element; the mode does not, so it is read off the controller
        const formId = (form.dataset?.id || form.getAttribute("data-id") || "").trim().toLowerCase();

        if (formId !== id) {
            return false;
        }

        const instance = webexpress.webui.Controller.getInstanceByElement(form);

        return !(instance && instance.mode === "delete");
    }

    /**
     * Decides whether a form is the dialog of a transition, which carries the target state.
     * @param {HTMLElement} form - The form.
     * @returns {boolean} True for the transition dialog.
     */
    isTransitionForm(form) {
        const selector = `[name="${this.TRANSITION_STATE_INPUT}"]`;

        return !!(form?.querySelector?.(selector) || form?.closest?.(".modal, .wx-webui-modal")?.querySelector(selector));
    }

    /**
     * Takes over a click on an entry of the status menu.
     * @param {MouseEvent} event - The click.
     */
    onClick(event) {
        // a click that asks for a new tab or window is the browser's to handle
        if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
            return;
        }

        const link = event.target?.closest?.("a[href]");
        const path = link ? this.transitionPath() : null;

        if (!path) {
            return;
        }

        let url;
        try {
            url = new URL(link.getAttribute("href"), window.location.href);
        } catch (_) {
            return;
        }

        if (url.pathname.toLowerCase() !== path) {
            return;
        }

        event.preventDefault();

        // one move at a time: a second click while the first is under way would ask for a move
        // out of a state the object has already left
        if (this._running) {
            return;
        }

        // the menu stays open while its anchor is pending otherwise
        try {
            link.closest("[popover]")?.hidePopover?.();
        } catch (_) {
            // a popover that is closed already
        }

        // the address is built with the server's external uri; it is requested on the origin
        // the page was served from, which is the same server
        this.enqueue(() => this.transition(url.pathname + url.search));
    }

    /**
     * Runs a step after the ones before it, so two refreshes never overlay the page at once.
     * @param {Function} step - The step, answering a promise.
     */
    enqueue(step) {
        this._queue = this._queue
            .then(async () => {
                this._running = true;
                document.documentElement.style.cursor = "progress";

                try {
                    await step();
                } finally {
                    this._running = false;
                    document.documentElement.style.cursor = "";
                }
            })
            .catch((error) => {
                console.error("Bringing the page up to date failed", error);
                window.location.reload();
            });
    }

    /**
     * Runs a transition through its endpoint and lays the page it redirects to over this one.
     * @param {string} address - The address of the transition endpoint.
     * @returns {Promise<void>} Settles once the page is up to date.
     */
    async transition(address) {
        await this.baseline();

        // the endpoint moves the object and redirects to the page it was asked from; the
        // request follows the redirect, so the answer is this page with the new state
        const result = await webexpress.webui.Transport.request(address);

        this.apply(result);
    }

    /**
     * Fetches this page again and lays it over the one on screen.
     * @returns {Promise<void>} Settles once the page is up to date.
     */
    async refresh() {
        await this.baseline();

        this.apply(await webexpress.webui.Transport.request(window.location.href));
    }

    /**
     * Provides the server markup the page on screen was built from, when it could not be kept
     * at load time. Fetched before the change, so the comparison shows only what the change did.
     * @returns {Promise<void>} Settles once the copy is there.
     */
    async baseline() {
        if (this._snapshot) {
            return;
        }

        const result = await webexpress.webui.Transport.request(window.location.href);
        const doc = this.parse(result);

        this._snapshot = doc ? this.capture(doc) : new Map();
    }

    /**
     * Lays the page of an answer over the page on screen. An answer that is no page of this
     * object - a refusal, a sign-in, an object that moved - is followed instead.
     * @param {object} result - The answer of the transport.
     */
    apply(result) {
        const doc = this.parse(result);

        if (!doc || this.objectId(doc) !== this.objectId()) {
            this.follow(result);
            return;
        }

        const final = this.finalUrl(result);
        if (final && final.pathname.toLowerCase() !== window.location.pathname.toLowerCase()) {
            this.follow(result);
            return;
        }

        for (const id of this.REGIONS) {
            const live = document.getElementById(id);
            const after = doc.getElementById(id);

            if (!live || !after) {
                continue;
            }

            const before = this._snapshot?.get(id);

            if (before) {
                this.patch(live, before, after);
            } else {
                this.replace(live, after);
            }
        }

        if (doc.title) {
            document.title = doc.title;
        }

        this._snapshot = this.capture(doc);
    }

    /**
     * Shows what an answer that cannot be laid over the page leads to.
     * @param {object} result - The answer of the transport.
     */
    follow(result) {
        const final = this.finalUrl(result);

        // the endpoint itself is never navigated to again, it would run the move a second time
        if (final && this.transitionPath() !== final.pathname.toLowerCase() && final.origin === window.location.origin) {
            window.location.assign(final.href);
            return;
        }

        window.location.reload();
    }

    /**
     * Reads the address a request ended on, after its redirects.
     * @param {object} result - The answer of the transport.
     * @returns {URL|null} The address, or null when the answer names none.
     */
    finalUrl(result) {
        const url = result?.response?.url;

        try {
            return url ? new URL(url, window.location.href) : null;
        } catch (_) {
            return null;
        }
    }

    /**
     * Parses the page of an answer.
     * @param {object} result - The answer of the transport.
     * @returns {Document|null} The page, or null when the answer carries none.
     */
    parse(result) {
        const html = result?.ok && typeof result?.data?.text === "string" ? result.data.text : null;

        return html ? new DOMParser().parseFromString(html, "text/html") : null;
    }

    /**
     * Collects the regions of a page.
     * @param {Document} doc - The page.
     * @param {Function} [copy] - Makes the kept copy of a region; the region itself by default.
     * @returns {Map<string, Element>} The regions by id.
     */
    capture(doc, copy = (element) => element) {
        const regions = new Map();

        for (const id of this.REGIONS) {
            const element = doc.getElementById(id);

            if (element) {
                regions.set(id, copy(element));
            }
        }

        return regions;
    }

    /**
     * Brings an element on screen from what the server rendered before to what it renders now.
     * The element is descended into while only its children with an id differ; anything else
     * that differs replaces it.
     * @param {Element} live - The element on screen.
     * @param {Element} before - The server markup it was built from.
     * @param {Element} after - The server markup it is to show now.
     */
    patch(live, before, after) {
        if (before.isEqualNode(after)) {
            return;
        }

        if (!this.isContainer(before) || !this.shell(before).isEqualNode(this.shell(after))) {
            this.replace(live, after);
            return;
        }

        // equal shells carry the same ids in the same order
        const next = this.slots(after);
        const pairs = this.slots(before).map((element, i) => [element, next[i]]);
        const targets = pairs.map(([element]) => document.getElementById(element.id));

        // a control may have moved a child out of its host or doubled an id; the host is then
        // the smallest part that is known to be where the markup says
        if (targets.some((target) => !target || !live.contains(target)) || new Set(targets).size !== targets.length) {
            this.replace(live, after);
            return;
        }

        pairs.forEach(([element, changed], i) => this.patch(targets[i], element, changed));
    }

    /**
     * Decides whether an element may be descended into: plain markup and the framing controls.
     * @param {Element} element - The server markup of the element.
     * @returns {boolean} True when its children can be brought up to date one by one.
     */
    isContainer(element) {
        const registry = webexpress.webui.Controller.classRegistry;

        return Array.from(element.classList)
            .filter((name) => registry?.has(name))
            .every((name) => this.CONTAINERS.includes(name));
    }

    /**
     * Lists the nearest descendants of an element that carry an id.
     * @param {Element} element - The element.
     * @returns {Element[]} The descendants in document order.
     */
    slots(element) {
        const found = [];
        const walk = (node) => {
            for (const child of node.children) {
                if (child.id) {
                    found.push(child);
                } else {
                    walk(child);
                }
            }
        };

        walk(element);

        return found;
    }

    /**
     * Copies an element with each of its nearest descendants with an id reduced to a marker
     * naming that id, so two copies are equal when the element differs only inside those.
     * @param {Element} element - The element.
     * @returns {Element} The copy.
     */
    shell(element) {
        const copy = element.cloneNode(true);

        for (const slot of this.slots(copy)) {
            slot.replaceWith(copy.ownerDocument.createComment(slot.id));
        }

        return copy;
    }

    /**
     * Replaces an element on screen by the server markup it is to show; the controller builds
     * the controls of the new markup and tears down those of the old.
     * @param {Element} live - The element on screen.
     * @param {Element} after - The server markup.
     */
    replace(live, after) {
        live.replaceWith(document.importNode(after, true));
    }
};
