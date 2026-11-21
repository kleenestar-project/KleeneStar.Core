var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * Opens the form of a transition when a card is dropped into a state whose transition asks for
 * input first, instead of letting the Kanban board store the drop.
 *
 * The board announces every save beforehand with a cancelable event (BEFORE_SAVE_EVENT, action
 * "move"). A card whose moves need a form carries a hidden marker in its markup
 * (RestApiObjectKindKanban.ScreenMarker): the address of the transition dialog per status key.
 * When the dropped status is one of them the save is refused - the board takes the card back -
 * and the dialog opens in the page's form modal. Its submit moves the object and stores the
 * entered values in one commit, then reloads the page, which shows the card in its new column.
 *
 * The boards belong to the framework, so this is no control of its own but a singleton
 * listening on the document.
 */
kleenestar.core.KanbanTransition = new class {
    /**
     * The attribute of the marker a card carries.
     */
    ATTRIBUTE = "data-ks-transition-screens";

    /**
     * The modal the dialog is shown in.
     */
    MODAL = "#modal-form";

    /**
     * Constructor
     */
    constructor() {
        const eventName = (typeof webexpress !== "undefined" && webexpress.webui?.Event?.BEFORE_SAVE_EVENT)
            || "webexpress.webui.save.before";

        document.addEventListener(eventName, (event) => this._onBeforeSave(event));
    }

    /**
     * Refuses the save of a drop whose transition shows a form and opens that form.
     * @param {CustomEvent} event - The announcement of the save.
     */
    _onBeforeSave(event) {
        const detail = event.detail || {};
        const payload = detail.payload;

        if (event.defaultPrevented || detail.action !== "move" || !payload || payload.statusId == null) {
            return;
        }

        const uri = this._screenUri(event.target, payload.cardId, payload.statusId);

        if (!uri) {
            return;
        }

        // the board takes the card back; the dialog moves it once its form is submitted
        event.preventDefault();
        this._open(uri);
    }

    /**
     * Reads the address of the dialog for a status out of the marker of a card.
     * @param {EventTarget} board - The board that announced the save.
     * @param {string} cardId - The id of the dropped card.
     * @param {string} statusId - The key of the status it was dropped into.
     * @returns {string|null} The address, or null when the move needs no input.
     */
    _screenUri(board, cardId, statusId) {
        if (!(board instanceof Element) || cardId == null) {
            return null;
        }

        const card = Array.from(board.querySelectorAll("[data-card-id]"))
            .find((element) => element.dataset.cardId === String(cardId));
        const marker = card?.querySelector(`[${this.ATTRIBUTE}]`);

        if (!marker) {
            return null;
        }

        try {
            const screens = JSON.parse(marker.getAttribute(this.ATTRIBUTE) || "{}");
            const uri = screens && screens[String(statusId)];

            return typeof uri === "string" && uri !== "" ? uri : null;
        } catch (error) {
            console.warn("kleenestar.core.KanbanTransition:", error);
            return null;
        }
    }

    /**
     * Shows the dialog in the form modal of the page, or opens it as a page of its own when the
     * page has no such modal.
     * @param {string} uri - The address of the dialog.
     */
    _open(uri) {
        const modal = webexpress.webui.Controller.getInstance(this.MODAL);

        if (modal && typeof modal.show === "function") {
            modal.size = "modal-lg";
            modal.uri = uri;
            modal.show();
            return;
        }

        window.location.assign(uri);
    }
};
