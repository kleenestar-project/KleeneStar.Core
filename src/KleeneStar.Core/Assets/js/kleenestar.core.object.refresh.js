var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * Keeps the view of an object up to date: its detail page, and the preview of a master-detail
 * pane (a frame that embeds the preview page of the selected row).
 *
 * The views are rendered by the server once. Three things change the record behind them:
 *
 * - A dialog that writes the record (the structured edit mask, the prose editor, the
 *   security-level and organize dialogs). A successful submit of a form that edits the object
 *   a detail page shows - the form's data-id is the object's id, so a dialog that writes
 *   something else on the page is left alone - reloads that page once the form has told
 *   everybody else. A delete is not followed, because the page would only reload into a "not
 *   found"; a form that is not in a dialog reports its own outcome in place. In a preview pane
 *   the pane is brought up to date instead.
 * - A workflow transition. The entries of the status menu link to the transition endpoint,
 *   which moves the object and redirects back; a plain click was a navigation that built the
 *   whole page again for a change of one badge. The click is taken over: the link is requested
 *   in the background and the view it ends on is laid over the one on screen. The transition
 *   dialog ends the same way.
 * - Another person, anywhere. Every commit of a record is announced on WebExpress's data
 *   change channel with the object's id; a view of that object fetches itself again, lays the
 *   result over the one on screen and flashes what changed, as a table does with a changed row.
 *
 * Laying over compares server markup with server markup. A detail page keeps the markup the
 * server sent from the moment this deferred script runs - the controller builds no control
 * before DOMContentLoaded - and a frame hands it over with every load (DATA_ARRIVED_EVENT);
 * after every refresh the markup just fetched takes its place. An element is replaced only
 * where the two differ, and only as a whole where a control owns it, so the selected tab, a
 * half-written comment, the scroll position and every section that did not change stay as they
 * are. A change from outside never replaces the element the reader is working in: it waits
 * until the focus has left it.
 */
kleenestar.core.ObjectRefresh = new class {
    /**
     * The meta tag a view names its object in.
     */
    META_NAME = "kleenestar.object";

    /**
     * The meta tag a view names the transition endpoint of its object in.
     */
    TRANSITION_META_NAME = "kleenestar.object.transition";

    /**
     * The meta tag a view names the change channel domain of objects in.
     */
    DOMAIN_META_NAME = "kleenestar.object.domain";

    /**
     * The hidden input the transition dialog carries the target state in.
     */
    TRANSITION_STATE_INPUT = "TransitionStateId";

    /**
     * The wire type of a change announcement (DataChangedMessageTypes.Changed).
     */
    CHANGED_TYPE = "webexpress.webapp.data.changed";

    /**
     * The regions of a detail page that show the record. Everything outside - the navigation,
     * the sidebar, the dialogs - is about the application, not about the object.
     */
    REGIONS = ["wx-breadcrumb", "wx-header-notifications", "wx-content"];

    /**
     * The controls that only frame their children and leave them in place, so a change inside
     * them is applied to the child that changed. Every other control owns the markup it was
     * built from and is replaced as a whole.
     */
    CONTAINERS = ["wx-webui-split", "wx-webui-section", "wx-webui-tab", "wx-webapp-collaborative"];

    /**
     * The class that flashes an element another person's change replaced (kleenestar.css).
     */
    FLASH_CLASS = "ks-change-flash";

    /**
     * How long the flash class stays on, matching its animation.
     */
    FLASH_MS = 1000;

    /**
     * How long announcements of one view are gathered before it fetches itself, so the commits
     * of one action - and the write that is still being finished - lead to one request.
     */
    SETTLE_MS = 300;

    /**
     * How long after an action of the reader's own a change is taken for theirs and not flashed.
     */
    QUIET_MS = 3000;

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
        this._frames = new Map();
        this._domains = new Set();
        this._timers = new Map();
        this._queue = Promise.resolve();
        this._running = false;
        this._quietUntil = 0;
        this._listener = null;

        // captured, so the link is taken over before anything else reacts to the click
        document.addEventListener("click", (event) => this.onClick(event), true);

        // a frame announces every page it loaded, with the markup; captured, because the
        // event is dispatched on the frame
        document.addEventListener(webexpress.webui.Event.DATA_ARRIVED_EVENT, (event) => this.onFrameArrived(event), true);

        document.addEventListener(webexpress.webui.Event.UPLOAD_SUCCESS_EVENT, (event) => this.onUploadSuccess(event));

        // the reader's own writes are announced like anybody else's; they are not flashed, and
        // a value saved in place already shows what its announcement would bring
        this._saved = [];
        document.addEventListener("submit", () => this.quiet(), true);
        document.addEventListener(webexpress.webui.Event.SAVED_EVENT, (event) => {
            this.quiet();

            if (event.target instanceof Element) {
                this._saved.push({ element: event.target, until: this._quietUntil });
            }
        }, true);

        if (this.objectId(document)) {
            this.listen(this.domain(document));
        }
    }

    /**
     * Reads the id of the object a view names.
     * @param {Document} [doc=document] - The page to read.
     * @returns {string|null} The id in lower case, or null on a page that names none.
     */
    objectId(doc = document) {
        return (this.meta(doc, this.META_NAME) || "").toLowerCase() || null;
    }

    /**
     * Reads the change channel domain of objects a view names.
     * @param {Document} doc - The page to read.
     * @returns {string|null} The domain, or null on a page that names none.
     */
    domain(doc) {
        return (this.meta(doc, this.DOMAIN_META_NAME) || "").toLowerCase() || null;
    }

    /**
     * Reads the path of the transition endpoint a view names.
     * @param {Document} [doc=document] - The page to read.
     * @returns {string|null} The path in lower case, or null on a page that names none.
     */
    transitionPath(doc = document) {
        const content = this.meta(doc, this.TRANSITION_META_NAME);

        if (!content) {
            return null;
        }

        try {
            return new URL(content, window.location.href).pathname.toLowerCase();
        } catch (_) {
            return null;
        }
    }

    /**
     * Reads a meta tag.
     * @param {Document} doc - The page.
     * @param {string} name - The name of the tag.
     * @returns {string|null} The trimmed content, or null.
     */
    meta(doc, name) {
        const content = (doc?.querySelector?.(`meta[name="${name}"]`)?.getAttribute("content") || "").trim();

        return content.length > 0 ? content : null;
    }

    /**
     * Reads the object a successful submit of a dialog edited.
     * @param {CustomEvent} event - The upload success event of a form.
     * @returns {string|null} The id in lower case, or null when the submit is not followed.
     */
    editedObject(event) {
        const form = event?.detail?.form;

        if (!form || !form.closest) {
            return null;
        }

        // a create or clone answers the new object's page, which its wizard follows (success
        // action follow-uri); a reload here would race that navigation
        if (event?.detail?.response?.data?.created === true) {
            return null;
        }

        // a form that is not in a dialog reports its outcome where it stands. The nesting
        // runs both ways: the modal form controller puts the served form inside its dialog,
        // the editor dialog puts its dialog inside the form
        if (!form.closest(".modal, .wx-webui-modal") && !form.querySelector(".modal, .wx-webui-modal")) {
            return null;
        }

        // the id survives on the element; the mode does not, so it is read off the controller
        const formId = (form.dataset?.id || form.getAttribute("data-id") || "").trim().toLowerCase();
        const instance = webexpress.webui.Controller.getInstanceByElement(form);

        if (!formId || (instance && instance.mode === "delete")) {
            return null;
        }

        return formId;
    }

    /**
     * Decides whether a successful submit changed the object the detail page shows.
     * @param {CustomEvent} event - The upload success event of a form.
     * @returns {boolean} True when the page has to be brought up to date.
     */
    changesThisObject(event) {
        const id = this.objectId();
        const formId = this.editedObject(event);

        if (!id || formId === null || formId !== id) {
            return false;
        }

        return true;
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
     * Follows a successful submit of a dialog.
     * @param {CustomEvent} event - The upload success event of a form.
     */
    onUploadSuccess(event) {
        const formId = this.editedObject(event);

        if (formId === null) {
            return;
        }

        this.quiet();

        // the previews of the edited object are brought up to date in place
        for (const record of this._frames.values()) {
            if (record.objectId === formId) {
                this.enqueue(() => this.refreshFrame(record, false));
            }
        }

        if (!this.changesThisObject(event)) {
            return;
        }

        // the transition dialog ends like the status menu: the page is laid over, not reloaded
        if (this.isTransitionForm(event.detail.form)) {
            this.enqueue(() => this.refresh(false));
            return;
        }

        // the reload is deferred a tick so the form's own listeners - the dialog closing,
        // the toast, the editor ending its draft - run against the page they were written for
        window.setTimeout(() => window.location.reload(), 0);
    }

    /**
     * Keeps the markup a frame loaded when it is the preview of an object.
     * @param {CustomEvent} event - The data arrived event of a control.
     */
    onFrameArrived(event) {
        const element = event.target;
        const instance = element instanceof Element ? webexpress.webui.Controller.getInstanceByElement(element) : null;

        if (!instance || typeof webexpress.webui.FrameCtrl !== "function" || !(instance instanceof webexpress.webui.FrameCtrl)) {
            return;
        }

        const html = event.detail?.response;
        const doc = typeof html === "string" && html.length > 0 ? new DOMParser().parseFromString(html, "text/html") : null;
        const objectId = this.objectId(doc);

        // the frame shows something else now; whatever it showed before is no longer followed
        this._frames.delete(element);

        if (!doc || !objectId) {
            return;
        }

        // the frame embeds the region its selector names (the main content of the preview)
        const selector = instance._selector || "body";
        const content = doc.querySelector(selector);

        if (!content) {
            return;
        }

        this._frames.set(element, {
            element: element,
            instance: instance,
            uri: instance.uri,
            selector: selector,
            objectId: objectId,
            transitionPath: this.transitionPath(doc),
            content: content
        });

        this.listen(this.domain(doc));
    }

    /**
     * Takes over a click on an entry of a status menu, on the detail page or in a preview.
     * @param {MouseEvent} event - The click.
     */
    onClick(event) {
        // a click that asks for a new tab or window is the browser's to handle
        if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
            return;
        }

        const link = event.target?.closest?.("a[href]");

        if (!link) {
            return;
        }

        // a link inside a preview belongs to the object of that preview
        const record = Array.from(this._frames.values()).find((item) => item.element.contains(link)) || null;
        const path = record ? record.transitionPath : this.transitionPath();

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

        // a native menu stays open while its anchor is pending otherwise
        try {
            link.closest("[popover]")?.hidePopover?.();
        } catch (_) {
            // a popover that is closed already
        }

        this.quiet();

        // the address is built with the server's external uri; it is requested on the origin
        // the page was served from, which is the same server
        const address = url.pathname + url.search;

        this.enqueue(() => record ? this.transitionFrame(record, address) : this.transition(address));
    }

    /**
     * Runs a step after the ones before it, so two refreshes never lay over a view at once.
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
                console.error("Bringing the view up to date failed", error);
                window.location.reload();
            });
    }

    /**
     * Marks the reader's own action, whose announcement follows shortly and is not flashed.
     */
    quiet() {
        this._quietUntil = Date.now() + this.QUIET_MS;
    }

    /**
     * Subscribes to the announcements of a domain on the data change channel.
     * @param {string|null} domain - The domain.
     */
    listen(domain) {
        const queue = webexpress.webapp?.MessageQueue;

        if (!domain || !queue || this._domains.has(domain)) {
            return;
        }

        this._domains.add(domain);

        if (!this._listener && typeof queue.register === "function") {
            this._listener = (payload) => this.onMessage(payload);
            queue.register(this._listener);
        }

        if (typeof queue.subscribeDomains === "function") {
            queue.subscribeDomains([domain]);
        }
    }

    /**
     * Follows an announcement of the data change channel: the views of the changed object - of
     * every object, when the announcement names none - fetch themselves again.
     * @param {*} payload - The message.
     */
    onMessage(payload) {
        if (!payload || typeof payload !== "object" || payload.type !== this.CHANGED_TYPE) {
            return;
        }

        const domain = typeof payload.domain === "string" ? payload.domain.toLowerCase() : null;

        if (!domain || !this._domains.has(domain)) {
            return;
        }

        const id = String(payload.itemId ?? payload.ItemId ?? "").trim().toLowerCase();
        const pageId = this.objectId();

        if (pageId && (!id || id === pageId)) {
            this.schedule("page", () => this.refresh(true));
        }

        for (const record of this._frames.values()) {
            if (!id || id === record.objectId) {
                this.schedule(record.element, () => this.refreshFrame(record, true));
            }
        }
    }

    /**
     * Gathers the announcements of one view, so it fetches itself once they have settled.
     * @param {*} key - The view.
     * @param {Function} step - The refresh of the view.
     */
    schedule(key, step) {
        window.clearTimeout(this._timers.get(key));

        this._timers.set(key, window.setTimeout(() => {
            this._timers.delete(key);
            this.enqueue(step);
        }, this.SETTLE_MS));
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
        this.apply(await webexpress.webui.Transport.request(address), false);
    }

    /**
     * Runs a transition of a previewed object and lays its new preview over the pane.
     * @param {object} record - The preview.
     * @param {string} address - The address of the transition endpoint.
     * @returns {Promise<void>} Settles once the preview is up to date.
     */
    async transitionFrame(record, address) {
        // the endpoint redirects to the page it was asked from; asked on behalf of the preview,
        // its answer is the preview with the new state
        const result = await webexpress.webui.Transport.request(address, { referrer: this.local(record.uri) });

        // an answer that is not the preview (the redirect went to the hosting page) still ran
        // the move; the preview is then fetched on its own
        if (!this.applyFrame(record, result, false)) {
            await this.refreshFrame(record, false);
        }
    }

    /**
     * Fetches the detail page again and lays it over the one on screen.
     * @param {boolean} external - Whether the change came from outside, and flashes.
     * @returns {Promise<void>} Settles once the page is up to date.
     */
    async refresh(external) {
        await this.baseline();

        this.apply(await webexpress.webui.Transport.request(window.location.href), external);
    }

    /**
     * Fetches a preview again and lays it over the pane.
     * @param {object} record - The preview.
     * @param {boolean} external - Whether the change came from outside, and flashes.
     * @returns {Promise<void>} Settles once the preview is up to date.
     */
    async refreshFrame(record, external) {
        if (!this.isCurrent(record)) {
            return;
        }

        const result = await webexpress.webui.Transport.request(this.local(record.uri), { credentials: "same-origin" });

        if (!this.applyFrame(record, result, external) && this.isCurrent(record)) {
            // no preview came back - an error, an object that is gone; the frame shows it
            record.instance.refresh();
        }
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

        const doc = this.parse(await webexpress.webui.Transport.request(window.location.href));

        this._snapshot = doc ? this.capture(doc) : new Map();
    }

    /**
     * Lays the page of an answer over the detail page on screen. An answer that is no page of
     * this object - a refusal, a sign-in, an object that moved - is followed instead.
     * @param {object} result - The answer of the transport.
     * @param {boolean} external - Whether the change came from outside, and flashes.
     */
    apply(result, external) {
        const doc = this.parse(result);
        const final = this.finalUrl(result);

        if (!doc || this.objectId(doc) !== this.objectId()
            || (final && final.pathname.toLowerCase() !== window.location.pathname.toLowerCase())) {
            // a change from outside that leads away is not followed; the reader keeps the page
            if (!external) {
                this.follow(result);
            }
            return;
        }

        const operations = [];

        for (const id of this.REGIONS) {
            const live = document.getElementById(id);
            const after = doc.getElementById(id);

            if (!live || !after) {
                continue;
            }

            const before = this._snapshot?.get(id);

            if (before) {
                this.plan(live, before, after, operations);
            } else {
                operations.push({ live: live, after: after });
            }
        }

        if (external && this.defer(operations, () => this.schedule("page", () => this.refresh(true)))) {
            return;
        }

        this.execute(operations, external);

        if (doc.title) {
            document.title = doc.title;
        }

        this._snapshot = this.capture(doc);
    }

    /**
     * Lays the page of an answer over a preview pane.
     * @param {object} record - The preview.
     * @param {object} result - The answer of the transport.
     * @param {boolean} external - Whether the change came from outside, and flashes.
     * @returns {boolean} True when the answer was the preview of the object and was applied.
     */
    applyFrame(record, result, external) {
        const doc = this.parse(result);
        const after = doc?.querySelector(record.selector);

        if (!after || this.objectId(doc) !== record.objectId) {
            return false;
        }

        // the reader selected another row meanwhile; that load answers for the pane
        if (!this.isCurrent(record)) {
            return true;
        }

        const operations = [];

        this.plan(record.element, record.content, after, operations, true);

        if (external && this.defer(operations, () => this.schedule(record.element, () => this.refreshFrame(record, true)))) {
            return true;
        }

        this.execute(operations, external);

        record.content = after;
        record.transitionPath = this.transitionPath(doc);

        return true;
    }

    /**
     * Decides whether a preview still shows what it showed when it was kept.
     * @param {object} record - The preview.
     * @returns {boolean} True while the frame shows the same page.
     */
    isCurrent(record) {
        return this._frames.get(record.element) === record
            && record.element.isConnected
            && record.instance.uri === record.uri
            && !record.instance.loading;
    }

    /**
     * Holds a change from outside back while it would replace the element the reader is
     * working in, and tries again once the focus has left it.
     * @param {Array<object>} operations - The planned replacements.
     * @param {Function} retry - Tries again.
     * @returns {boolean} True when the change is held back.
     */
    defer(operations, retry) {
        const active = document.activeElement;

        if (!active || active === document.body || !operations.some((operation) => operation.live.contains(active))) {
            return false;
        }

        active.addEventListener("focusout", () => retry(), { once: true });

        return true;
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
     * Turns an address of this application into one on the origin the page was served from.
     * @param {string} uri - The address, possibly built with the server's external uri.
     * @returns {string} The address on this origin.
     */
    local(uri) {
        try {
            const url = new URL(uri, window.location.href);

            return window.location.origin + url.pathname + url.search;
        } catch (_) {
            return uri;
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
     * Collects the regions of a detail page.
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
     * Plans how an element on screen goes from what the server rendered before to what it
     * renders now. The element is descended into while only its children with an id differ;
     * anything else that differs replaces it.
     * @param {Element} live - The element on screen.
     * @param {Element} before - The server markup it was built from.
     * @param {Element} after - The server markup it is to show now.
     * @param {Array<object>} operations - Receives the replacements.
     * @param {boolean} [root=false] - Whether the element on screen is a host of the markup's
     * children rather than the element itself (a frame); its own attributes are not compared,
     * and it keeps itself when its content is replaced.
     */
    plan(live, before, after, operations, root = false) {
        if (before.isEqualNode(after)) {
            return;
        }

        const replace = () => operations.push({ live: live, after: after, root: root });

        if ((!root && !this.isContainer(before)) || !this.shell(before, root).isEqualNode(this.shell(after, root))) {
            replace();
            return;
        }

        // equal shells carry the same ids in the same order
        const next = this.slots(after);
        const pairs = this.slots(before).map((element, i) => [element, next[i]]);
        const targets = pairs.map(([element]) => this.find(live, element.id));

        // a control may have moved a child out of its host or doubled an id; the host is then
        // the smallest part that is known to be where the markup says
        if (targets.some((target) => !target)) {
            replace();
            return;
        }

        pairs.forEach(([element, changed], i) => this.plan(targets[i], element, changed, operations));
    }

    /**
     * Applies planned replacements; the controller builds the controls of the new markup and
     * tears down those of the old.
     * @param {Array<object>} operations - The replacements.
     * @param {boolean} flash - Whether the new elements flash.
     */
    execute(operations, flash) {
        const changed = [];
        const now = Date.now();

        this._saved = this._saved.filter((entry) => entry.until > now && entry.element.isConnected);

        for (const operation of operations) {
            // the announcement of a value the reader just saved in place: the control shows it
            // already, and rebuilding it would take the reader out of the sheet they work in
            if (flash && this._saved.some((entry) => operation.live.contains(entry.element))) {
                continue;
            }

            const tabs = this.selectedTabs(operation.live);
            let host;

            if (operation.root) {
                const nodes = Array.from(operation.after.childNodes).map((node) => document.importNode(node, true));

                operation.live.replaceChildren(...nodes);
                host = operation.live;
            } else {
                host = document.importNode(operation.after, true);
                operation.live.replaceWith(host);
            }

            changed.push(host);

            // the controller builds the new controls when it is told about the insertion,
            // which happens after this task; the tabs are selected again once it has
            if (tabs.length > 0) {
                window.setTimeout(() => this.selectTabs(host, tabs), 0);
            }
        }

        if (flash && now > this._quietUntil) {
            changed.forEach((element) => this.flash(element));
        }
    }

    /**
     * Reads which tab each tab control inside an element shows, in document order.
     * @param {Element} element - The element on screen.
     * @returns {number[]} The positions of the selected tabs.
     */
    selectedTabs(element) {
        return this.tabControls(element).map((instance) =>
            (instance.state?.tabs || []).findIndex((tab) => tab.id === instance.state?.activeTabId));
    }

    /**
     * Selects the tabs a replaced element showed in the tab controls of its replacement. The
     * tabs of a control without an id get new ids each time they are built, so they are
     * matched by position.
     * @param {Element} element - The replacement.
     * @param {number[]} positions - The positions of the selected tabs.
     */
    selectTabs(element, positions) {
        this.tabControls(element).forEach((instance, i) => {
            const tab = (instance.state?.tabs || [])[positions[i]];

            if (tab && typeof instance.selectTab === "function") {
                instance.selectTab(tab.id);
            }
        });
    }

    /**
     * Lists the tab controls inside an element, the element included, in document order.
     * @param {Element} element - The element.
     * @returns {Array<object>} The controller instances.
     */
    tabControls(element) {
        if (!element?.isConnected || typeof webexpress.webui.TabCtrl !== "function") {
            return [];
        }

        return [element, ...element.querySelectorAll(".wx-tab-container")]
            .map((node) => webexpress.webui.Controller.getInstanceByElement(node))
            .filter((instance) => instance instanceof webexpress.webui.TabCtrl);
    }

    /**
     * Flashes an element, restarting a flash that is still playing.
     * @param {Element} element - The element.
     */
    flash(element) {
        window.clearTimeout(element._ksFlashTimer);
        element.classList.remove(this.FLASH_CLASS);

        // reading a layout property commits the removal, so the animation starts again
        void element.offsetWidth;

        element.classList.add(this.FLASH_CLASS);
        element._ksFlashTimer = window.setTimeout(() => element.classList.remove(this.FLASH_CLASS), this.FLASH_MS);
    }

    /**
     * Finds the one element with an id inside an element on screen.
     * @param {Element} live - The element on screen.
     * @param {string} id - The id.
     * @returns {Element|null} The element, or null when there is none or more than one.
     */
    find(live, id) {
        const found = live.querySelectorAll(`[id="${id.replace(/["\\]/g, "\\$&")}"]`);

        return found.length === 1 ? found[0] : null;
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
     * @param {boolean} [bare=false] - Whether the element's own attributes are left out.
     * @returns {Element} The copy.
     */
    shell(element, bare = false) {
        const copy = element.cloneNode(true);

        for (const slot of this.slots(copy)) {
            slot.replaceWith(copy.ownerDocument.createComment(slot.id));
        }

        if (bare) {
            for (const attribute of Array.from(copy.attributes)) {
                copy.removeAttribute(attribute.name);
            }
        }

        return copy;
    }
};
