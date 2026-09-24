/**
 * KleeneStar: keeps the comment count in the header of the docked thread in step.
 *
 * The header of the thread (#object-comments-section) carries the number of comments as its
 * badge, so a folded thread still says whether there is a conversation. The server writes the
 * number when it renders the page, and nothing re-rendered the page when a comment was added,
 * answered or deleted - the badge went on showing the count the page was opened with. After each
 * of those the thread is read again from the endpoint the page names
 * (meta kleenestar.comments) and the badge set through the section's own badge property.
 *
 * It counts what the thread shows, the way the server does: comments and replies, not the
 * deleted ones the endpoint redacts (a deleted comment comes back in the category "Deleted", a
 * deleted reply with an empty body).
 */
(function () {
    const META_NAME = "kleenestar.comments";
    const SECTION_ID = "object-comments-section";

    /**
     * Reads the endpoint of the page's thread.
     * @returns {string|null} The address, or null on a page without a thread.
     */
    const endpoint = () => {
        const meta = document.querySelector(`meta[name="${META_NAME}"]`);
        const content = meta ? (meta.getAttribute("content") || "").trim() : "";

        return content.length > 0 ? content : null;
    };

    /**
     * Counts the visible comments and replies of a thread answer.
     * @param {any} data The answer of the comments endpoint.
     * @returns {number} The count.
     */
    const count = (data) => {
        const items = Array.isArray(data) ? data : (Array.isArray(data?.items) ? data.items : (Array.isArray(data?.data) ? data.data : []));

        return items.reduce((sum, item) => {
            const own = item && item.category !== "Deleted" ? 1 : 0;
            const replies = Array.isArray(item?.replies) ? item.replies.filter((r) => r && r.body).length : 0;

            return sum + own + replies;
        }, 0);
    };

    /**
     * Reads the thread again and puts its count on the section's badge.
     */
    const refresh = async () => {
        const uri = endpoint();
        const section = document.getElementById(SECTION_ID);
        const controller = window.webexpress?.webui?.Controller;
        const instance = section && controller && typeof controller.getInstanceByElement === "function"
            ? controller.getInstanceByElement(section)
            : null;

        if (!uri || !instance) {
            return;
        }

        try {
            const response = await fetch(uri, { headers: { "Accept": "application/json" }, credentials: "same-origin" });

            if (response.ok) {
                instance.badge = String(count(await response.json()));
            }
        } catch {
            // a count that could not be read keeps the one the page had
        }
    };

    const events = window.webexpress?.webapp?.Event;

    if (!events) {
        return;
    }

    [events.COMMENT_ADDED_EVENT, events.COMMENT_REPLY_EVENT, events.COMMENT_DELETED_EVENT]
        .filter(Boolean)
        .forEach((name) => document.addEventListener(name, () => window.setTimeout(refresh, 250)));
})();
