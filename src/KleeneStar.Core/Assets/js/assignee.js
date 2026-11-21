var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * Changes the assignee of an object from the people card without reloading the page.
 *
 * The action of the assignee row (ObjectPropertyPeopleCardFragment, a link with the class
 * ks-assignee-action) addresses /api/1/assignee/{objectkey}, with ?clear=1 to unassign. A click
 * sends that address as PUT; the endpoint changes the assignment and answers { html }, the row
 * rendered anew, which takes the place of the old one - with the action that now applies. When
 * the request fails the page is reloaded and shows the assignment as it is stored; the GET of
 * the address changes nothing and only leads to the object.
 *
 * The row belongs to a framework section, so this is no control of its own but a singleton
 * listening on the document.
 */
kleenestar.core.Assignee = new class {
    /**
     * The selector of the action link.
     */
    SELECTOR = "a.ks-assignee-action";

    /**
     * Constructor
     */
    constructor() {
        document.addEventListener("click", (event) => {
            const link = event.target && typeof event.target.closest === "function"
                ? event.target.closest(this.SELECTOR)
                : null;

            if (!link || event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey) {
                return;
            }

            event.preventDefault();
            void this.change(link);
        });
    }

    /**
     * Sends the change and swaps in the row the endpoint renders.
     * @param {HTMLAnchorElement} link - The action link.
     * @returns {Promise<void>} Settles when the row is replaced or the page navigates.
     */
    async change(link) {
        const row = link.closest(".ks-person");

        if (!row || link.getAttribute("aria-busy") === "true") {
            return;
        }

        link.setAttribute("aria-busy", "true");

        try {
            const response = await fetch(link.href, {
                method: "PUT",
                credentials: "same-origin",
                headers: { "Accept": "application/json" }
            });

            const data = response.ok ? await response.json() : null;

            if (!data || typeof data.html !== "string" || data.html === "") {
                throw new Error("the assignee was not changed");
            }

            const template = document.createElement("template");
            template.innerHTML = data.html.trim();

            const next = template.content.firstElementChild;

            if (!next) {
                throw new Error("the answer holds no row");
            }

            row.replaceWith(next);
        } catch (error) {
            console.warn("kleenestar.core.Assignee:", error);
            window.location.reload();
        }
    }
};
