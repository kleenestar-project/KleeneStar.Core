/**
 * KleeneStar: brings an object detail page up to date after a dialog has changed the object.
 *
 * The detail page of an issue, an asset, a document or a blog post is rendered by the server
 * once, and the dialogs that write the record - the structured edit mask, the prose editor,
 * the security-level and organize dialogs - only close when they are done. Nothing re-rendered
 * the description, the property cards or the relation card afterwards, so the page went on
 * showing the record as it was when it was opened; the change was only visible after a manual
 * reload. A workflow transition on the same page is a plain navigation that ends in a redirect
 * back to it, so a reload is already what the page does when its record changes - this makes
 * the dialogs do the same.
 *
 * The page names the object it shows in a meta tag (kleenestar.object). A successful submit of
 * a form that edits that very record - the form's data-id is the object's id, so a dialog that
 * writes something else on the page, a comment or a relation of another object, is left alone
 * - reloads the page once the form has told everybody else. A delete is not followed, because
 * the page would only reload into a "not found"; and a form that is not in a dialog is not
 * followed either, because a page-level form reports its own outcome in place.
 */
(function () {
    /**
     * The meta tag the detail page names its object in.
     */
    const META_NAME = "kleenestar.object";

    /**
     * Reads the id of the object the page shows.
     * @returns {string|null} The id, or null on a page that names none.
     */
    const objectId = () => {
        const meta = document.querySelector(`meta[name="${META_NAME}"]`);
        const content = meta ? (meta.getAttribute("content") || "").trim().toLowerCase() : "";

        return content.length > 0 ? content : null;
    };

    /**
     * Decides whether a successful submit changed the object the page shows.
     * @param {CustomEvent} event The upload success event of a form.
     * @returns {boolean} True when the page has to be reloaded.
     */
    const changesThisObject = (event) => {
        const form = event?.detail?.form;
        const id = objectId();

        if (!form || !id || !form.closest) {
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

        const controller = window.webexpress?.webui?.Controller;
        const instance = controller && typeof controller.getInstanceByElement === "function"
            ? controller.getInstanceByElement(form)
            : null;

        return !(instance && instance.mode === "delete");
    };

    const eventName = window.webexpress?.webui?.Event?.UPLOAD_SUCCESS_EVENT;

    if (!eventName) {
        return;
    }

    document.addEventListener(eventName, (event) => {
        if (!changesThisObject(event)) {
            return;
        }

        // the reload is deferred a tick so the form's own listeners - the dialog closing,
        // the toast, the editor ending its draft - run against the page they were written for
        window.setTimeout(() => window.location.reload(), 0);
    });
})();
