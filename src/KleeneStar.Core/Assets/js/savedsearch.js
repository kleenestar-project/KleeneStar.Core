var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * Saved searches on the global search page.
 *
 * The page's advanced search is the framework's SearchCtrl (WebControl/SavedSearchAdvancedSearch.cs),
 * which opens on the running saved search through data-wql and carries the save and share buttons
 * in its extended content. The search element belongs to the framework's controller, so this is
 * no control of its own but a singleton listening on the document.
 *
 * 1. Saving what is on screen. A dialog address is fixed when the page renders, while the query
 *    to save is whatever the user typed since. A button marked data-ks-savedsearch-carry gets the
 *    current expression appended to its dialog address on click, in the capture phase, before the
 *    framework's action handler reads the address. The expression is read from the controller,
 *    else from the change events the search emits; a basic term is saved as the WQL the basic
 *    search stands for, a match on the summary.
 *
 * 2. Showing the result. The sidebar and the headline are rendered once; after an edit, permission
 *    or delete dialog succeeds the page is reloaded, after a delete without the deleted search's id.
 *    A create answers data.created and is followed by its dialog (success action follow-uri).
 */
kleenestar.core.SavedSearch = new class {
    /**
     * The attribute marking a button that carries the current expression into its dialog; its
     * value is the query parameter the expression travels in.
     */
    CARRY_ATTRIBUTE = "data-ks-savedsearch-carry";

    /**
     * The expression last announced by a search host, keyed by the host.
     */
    _current = new WeakMap();

    /**
     * Constructor
     */
    constructor() {
        // the search re-emits every change as one unified event on its host; the last one is
        // what the table shows and therefore what a save stores
        document.addEventListener(webexpress.webui.Event.CHANGE_FILTER_EVENT, (event) => {
            const host = event.target?.closest?.("[data-ks-savedsearch-host]");

            if (host && event.detail && !event.detail._fromAdvanced) {
                this._current.set(host, { value: event.detail.value || "", searchType: event.detail.searchType || "basic" });
            }
        });

        // capture phase: the address has to be rewritten before the action handler reads it
        document.addEventListener("click", (event) => {
            const button = event.target?.closest?.("[" + this.CARRY_ATTRIBUTE + "]");

            if (button) {
                this.carry(button);
            }
        }, true);

        document.addEventListener(webexpress.webui.Event.UPLOAD_SUCCESS_EVENT, (event) => this._onSuccess(event));
    }

    /**
     * Quotes a term as a WQL string literal.
     * @param {string} term - The term.
     * @returns {string} The literal.
     */
    quote(term) {
        return "\"" + String(term).replace(/\\/g, "\\\\").replace(/"/g, "\\\"") + "\"";
    }

    /**
     * Turns what a search announced into the WQL expression it stands for.
     * @param {{value: string, searchType: string}} state - The announced search.
     * @returns {string} The expression, empty when there is none.
     */
    toWql(state) {
        const value = (state?.value || "").trim();

        if (!value) {
            return "";
        }

        // the basic search matches the summary (Objects/Table.Filter); saved as WQL it is the
        // same question the search page answered
        return state.searchType === "wql" ? value : "Summary ~ " + this.quote(value);
    }

    /**
     * Returns the search host a button belongs to. The buttons stand in the host's extended
     * content; an entry of the "..." menu may be drawn outside it, and the page has one search.
     * @param {HTMLElement} button - The button.
     * @returns {HTMLElement|null} The host.
     */
    hostOf(button) {
        return button.closest("[data-ks-savedsearch-host]")
            || document.querySelector("[data-ks-savedsearch-host]");
    }

    /**
     * Reads what a search host shows right now - including a term typed but not yet submitted -
     * from its controller, falling back to the last change it announced.
     * @param {HTMLElement} host - The search host.
     * @returns {{value: string, searchType: string}|undefined} The search on screen.
     */
    onScreen(host) {
        const instance = webexpress.webui.Controller.getInstanceByElement(host);

        if (instance && instance._initialMode === "wql" && instance._wqlCtrl) {
            return { value: instance._wqlCtrl.value || "", searchType: "wql" };
        }

        if (instance && instance._initialMode === "basic" && instance._basicCtrl && typeof instance._basicCtrl.value === "string") {
            return { value: instance._basicCtrl.value, searchType: "basic" };
        }

        return this._current.get(host);
    }

    /**
     * Appends the current expression to the dialog address of a carrying button.
     * @param {HTMLElement} button - The button.
     */
    carry(button) {
        const parameter = button.getAttribute(this.CARRY_ATTRIBUTE) || "wql";
        const host = this.hostOf(button);
        const wql = host ? this.toWql(this.onScreen(host)) : "";

        ["primary", "secondary"].forEach((prefix) => {
            const attribute = "data-wx-" + prefix + "-uri";
            const uri = button.getAttribute(attribute);

            if (!uri) {
                return;
            }

            const url = new URL(uri, window.location.origin);
            url.searchParams.delete(parameter);

            if (wql) {
                url.searchParams.set(parameter, wql);
            }

            button.setAttribute(attribute, url.pathname + url.search);
        });
    }

    /**
     * Shows the result of a saved-search dialog: reloads the page, or leaves a deleted search.
     * @param {CustomEvent} event - The upload success event of a form.
     */
    _onSuccess(event) {
        const form = event?.detail?.form;

        // a create is followed by its dialog; a form outside a dialog reports where it stands
        if (event?.detail?.response?.data?.created === true || !form || !form.closest) {
            return;
        }

        if (!form.closest(".modal, .wx-webui-modal") && !form.querySelector(".modal, .wx-webui-modal")) {
            return;
        }

        // a deleted saved search cannot be run any more; the plain search page is what is
        // left of it (a reload would do the same, but keep its id in the address)
        const instance = webexpress.webui.Controller.getInstanceByElement(form);

        if (instance && instance.mode === "delete") {
            window.setTimeout(() => window.location.assign(window.location.pathname), 0);
            return;
        }

        window.setTimeout(() => window.location.reload(), 0);
    }
};
