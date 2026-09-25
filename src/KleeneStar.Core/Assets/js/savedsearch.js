/**
 * KleeneStar saved searches on the global search page.
 *
 * The page's advanced search is the framework's SearchCtrl, and three things a saved search
 * needs are not in its contract; they are done here, on attributes the server writes
 * (WebControl/SavedSearchAdvancedSearch.cs):
 *
 * 1. Opening on a saved query. The control seeds only its basic box (data-value) and restores
 *    whichever mode the user left it in. A search host carrying data-ks-wql is switched to the
 *    WQL prompt and given that expression, so running a saved search shows the query it runs.
 *    The results table is seeded with the same expression on the server, so the first paint
 *    is already right; switching the mode re-announces the query, which re-queries the table
 *    with what it already shows.
 *
 * 2. Saving what is on screen. A dialog address is fixed when the page renders, while the
 *    query to save is whatever the user typed since. A button marked data-ks-savedsearch-carry
 *    gets the current expression appended to its dialog address the moment it is clicked -
 *    before the framework's action handler reads the address. The expression is tracked from
 *    the change events the search emits (a public contract); a basic term is saved as the WQL
 *    the basic search stands for, a match on the summary.
 *
 * 3. Showing the result. The sidebar and the headline are rendered once; after a saved-search
 *    dialog succeeds the page is reloaded - after a delete, without the deleted search's id. A create is left to objectcreated.js, which follows
 *    the answer to the new saved search.
 */
(function () {
    /**
     * The attribute naming the expression a search host opens with.
     */
    const WQL_ATTRIBUTE = "data-ks-wql";

    /**
     * The attribute marking a button that carries the current expression into its dialog; its
     * value is the query parameter the expression travels in.
     */
    const CARRY_ATTRIBUTE = "data-ks-savedsearch-carry";

    /**
     * The expression last announced by a search host, keyed by the host.
     */
    const current = new WeakMap();

    /**
     * Returns the controller registry, or null on a page without the framework runtime.
     * @returns {object|null} The registry.
     */
    const controller = () => window.webexpress?.webui?.Controller || null;

    /**
     * Quotes a term as a WQL string literal.
     * @param {string} term The term.
     * @returns {string} The literal.
     */
    const quote = (term) => "\"" + String(term).replace(/\\/g, "\\\\").replace(/"/g, "\\\"") + "\"";

    /**
     * Turns what a search announced into the WQL expression it stands for.
     * @param {{value: string, searchType: string}} state The announced search.
     * @returns {string} The expression, empty when there is none.
     */
    const toWql = (state) => {
        const value = (state?.value || "").trim();

        if (!value) {
            return "";
        }

        // the basic search matches the summary (Objects/Table.Filter); saved as WQL it is the
        // same question the search page answered
        return state.searchType === "wql" ? value : "Summary ~ " + quote(value);
    };

    /**
     * Puts a search host into WQL mode with an expression, once its controller exists.
     * @param {HTMLElement} host The search host.
     * @param {string} wql The expression.
     * @param {number} attempt The number of tries so far.
     */
    const seed = (host, wql, attempt) => {
        const registry = controller();
        const instance = registry && typeof registry.getInstanceByElement === "function"
            ? registry.getInstanceByElement(host)
            : null;

        // the controllers are built when the page is ready; a script included earlier waits
        if (!instance) {
            if (attempt < 50) {
                window.setTimeout(() => seed(host, wql, attempt + 1), 50);
            }

            return;
        }

        // the embedded prompt and the mode switch are the control's own members; guarded, so a
        // framework that renames them leaves the page on the server-seeded results
        if (instance._wqlCtrl && typeof instance._applyMode === "function") {
            // switching modes announces the prompt's text as the new query, but it reads it off
            // the editable box's `value`, which a div does not have - the table would be
            // re-queried with no expression at all. The table is seeded with this expression on
            // the server already, so the switch is made quietly, the way the control makes its
            // own first one.
            const initializing = instance._isInitializing;
            instance._isInitializing = true;

            try {
                instance._applyMode("wql");
            } finally {
                instance._isInitializing = initializing;
            }

            instance._wqlCtrl.value = wql;
        }

        current.set(host, { value: wql, searchType: "wql" });
    };

    /**
     * Returns the search host a button belongs to. The buttons stand beside the search in one
     * bar, because the framework control clears whatever is rendered inside it.
     * @param {HTMLElement} button The button.
     * @returns {HTMLElement|null} The host.
     */
    const hostOf = (button) => button.closest(".ks-savedsearch-bar")?.querySelector("[data-ks-savedsearch-host]")
        // an entry of the "..." menu may be drawn outside the bar; the page has one search
        || document.querySelector("[data-ks-savedsearch-host]");

    /**
     * Reads what a search host shows right now - including a term typed but not yet submitted -
     * from its controller, falling back to the last change it announced.
     * @param {HTMLElement} host The search host.
     * @returns {{value: string, searchType: string}|undefined} The search on screen.
     */
    const onScreen = (host) => {
        const registry = controller();
        const instance = registry && typeof registry.getInstanceByElement === "function"
            ? registry.getInstanceByElement(host)
            : null;

        if (instance && instance._initialMode === "wql" && instance._wqlCtrl) {
            return { value: instance._wqlCtrl.value || "", searchType: "wql" };
        }

        if (instance && instance._initialMode === "basic" && instance._basicCtrl && typeof instance._basicCtrl.value === "string") {
            return { value: instance._basicCtrl.value, searchType: "basic" };
        }

        return current.get(host);
    };

    /**
     * Appends the current expression to the dialog address of a carrying button.
     * @param {HTMLElement} button The button.
     */
    const carry = (button) => {
        const parameter = button.getAttribute(CARRY_ATTRIBUTE) || "wql";
        const host = hostOf(button);
        const wql = host ? toWql(onScreen(host)) : "";

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
    };

    const ready = () => {
        document.querySelectorAll("[" + WQL_ATTRIBUTE + "]").forEach((host) => {
            const wql = host.getAttribute(WQL_ATTRIBUTE) || "";

            current.set(host, { value: wql, searchType: "wql" });

            if (wql) {
                seed(host, wql, 0);
            }
        });
    };

    // the search re-emits every change as one unified event on its host; the last one is what
    // the table shows and therefore what a save stores
    const changeEvent = window.webexpress?.webui?.Event?.CHANGE_FILTER_EVENT;

    if (changeEvent) {
        document.addEventListener(changeEvent, (event) => {
            const host = event.target?.closest?.("[data-ks-savedsearch-host]");

            if (host && event.detail && !event.detail._fromAdvanced) {
                current.set(host, { value: event.detail.value || "", searchType: event.detail.searchType || "basic" });
            }
        });
    }

    // capture phase: the address has to be rewritten before the action handler reads it
    document.addEventListener("click", (event) => {
        const button = event.target?.closest?.("[" + CARRY_ATTRIBUTE + "]");

        if (button) {
            carry(button);
        }
    }, true);

    const successEvent = window.webexpress?.webui?.Event?.UPLOAD_SUCCESS_EVENT;

    if (successEvent) {
        document.addEventListener(successEvent, (event) => {
            const form = event?.detail?.form;

            // a create is followed by objectcreated.js; a form outside a dialog reports where
            // it stands
            if (event?.detail?.response?.data?.created === true || !form || !form.closest) {
                return;
            }

            if (!form.closest(".modal, .wx-webui-modal") && !form.querySelector(".modal, .wx-webui-modal")) {
                return;
            }

            // a deleted saved search cannot be run any more; the plain search page is what is
            // left of it (a reload would do the same, but keep its id in the address)
            const registry = controller();
            const instance = registry && typeof registry.getInstanceByElement === "function"
                ? registry.getInstanceByElement(form)
                : null;

            if (instance && instance.mode === "delete") {
                window.setTimeout(() => window.location.assign(window.location.pathname), 0);
                return;
            }

            window.setTimeout(() => window.location.reload(), 0);
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", ready);
    } else {
        ready();
    }
})();
