var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * KleeneStar object-move-tree controller.
 *
 * Wraps a movable webexpress.webui tree (data-movable="true") whose nodes are objects of a
 * single workspace and persists every drag-and-drop re-parent to the server. The wrapper
 * element carries the target endpoint in its "data-rest-uri" attribute.
 *
 * The base tree control (webexpress.webui.tree) already performs the client-side move and
 * dispatches a bubbling webexpress.webui.move event ({ node, target, position, sender, ... });
 * it does NOT persist. This controller listens for that event, reads where the dragged node
 * actually landed in the freshly rendered DOM (which yields the correct new parent for every
 * drop position: above, below or child) and POSTs { node, parent } to the move endpoint. On a
 * rejected move (e.g. a hierarchy-rule violation returning 400) the page is reloaded so the
 * optimistic client-side move is reverted to the persisted state.
 */
kleenestar.core.ObjectMoveTreeCtrl = class extends webexpress.webui.Ctrl {
    /**
     * Initializes the controller: caches the REST endpoint and subscribes to move events.
     * @param {HTMLElement} element - The wrapper element hosting the movable tree.
     */
    constructor(element) {
        super(element);

        this._restUri = element.getAttribute("data-rest-uri");
        element.removeAttribute("data-rest-uri");

        this._onMove = (event) => this._handleMove(event);
        document.addEventListener(webexpress.webui.Event.MOVE_EVENT, this._onMove);
    }

    /**
     * Handles a tree move: ignores moves from foreign trees, then persists the dragged node's
     * new parent once the tree has re-rendered.
     * @param {CustomEvent} event - The move event dispatched by the tree control.
     */
    _handleMove(event) {
        const detail = event && event.detail ? event.detail : {};

        // only react to moves originating from the tree hosted inside this wrapper
        if (!detail.sender || !this._element.contains(detail.sender)) {
            return;
        }

        const nodeId = detail.node;
        if (!nodeId) {
            return;
        }

        // the tree dispatches the event before it re-renders; defer so the DOM reflects the
        // dropped position, then read the dragged node's new parent from its ancestor list item.
        window.setTimeout(() => {
            const li = this._findNodeElement(nodeId);
            let parentId = null;

            if (li && li.parentElement) {
                const parentLi = li.parentElement.closest("li");
                parentId = parentLi ? parentLi.id : null;
            }

            this._persist(nodeId, parentId);
        }, 0);
    }

    /**
     * Locates the rendered list item of a node by its id, escaping the id for the selector.
     * @param {string} id - The node id (an object key).
     * @returns {HTMLElement|null} The list item element, or null when not found.
     */
    _findNodeElement(id) {
        const selector = "#" + (window.CSS && CSS.escape ? CSS.escape(id) : id);
        return this._element.querySelector(selector);
    }

    /**
     * Sends the move to the server. Reloads the page when the server rejects it so the tree
     * reverts to the persisted hierarchy.
     * @param {string} node - The moved object's key.
     * @param {string|null} parent - The new parent object's key, or null when moved to the root.
     */
    _persist(node, parent) {
        fetch(this._restUri, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ node: node, parent: parent })
        })
            .then((response) => {
                if (!response.ok) {
                    window.location.reload();
                }
            })
            .catch(() => {
                window.location.reload();
            });
    }

    /**
     * Detaches the document-level move listener when the control is destroyed.
     */
    destroy() {
        document.removeEventListener(webexpress.webui.Event.MOVE_EVENT, this._onMove);
        super.destroy();
    }
};

// register the class in the controller
webexpress.webui.Controller.registerClass("wx-kleenestar-object-movetree", kleenestar.core.ObjectMoveTreeCtrl);
