var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * Opens an object once it has been created, so the page around it shows it.
 *
 * The create wizard and the clone dialog only close when they are done, while the page behind
 * them is rendered once by the server - the page tree in a document sidebar, the recent lists,
 * the overview a clone was started from. Nothing re-rendered them, so a new document was missing
 * from the tree until the page was reloaded by hand. /api/1/objects answers a create with the
 * new object ({ created: true, id, key, uri }); this follows that answer to the object's page,
 * which renders the sidebar anew with the object in it and the object open.
 *
 * Only an answer that says "created" is followed, so an edit, a delete or any other form in a
 * dialog is left to its own handling.
 */
kleenestar.core.ObjectCreated = new class {
    /**
     * Constructor
     */
    constructor() {
        document.addEventListener(webexpress.webui.Event.UPLOAD_SUCCESS_EVENT, (event) => {
            const uri = this.createdUri(event);

            if (!uri) {
                return;
            }

            // deferred a tick so the form's own listeners - the dialog closing, the toast - run
            // against the page they were written for before it is left
            window.setTimeout(() => window.location.assign(uri), 0);
        });
    }

    /**
     * Reads the address of the object a successful submit created.
     * @param {CustomEvent} event - The upload success event of a form.
     * @returns {string|null} The address, or null when the submit created no object.
     */
    createdUri(event) {
        const response = event?.detail?.response;
        const data = response && typeof response === "object" ? (response.data || null) : null;

        if (!data || data.created !== true || typeof data.uri !== "string" || data.uri.length === 0) {
            return null;
        }

        return data.uri;
    }
};
