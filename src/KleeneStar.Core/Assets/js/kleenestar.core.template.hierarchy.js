var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * The node tree of a template hierarchy in the hierarchy wizard: the existing templates whose
 * objects are created with the object of the root template, each below its parent node as a
 * sub-object, linked to other objects of the structure, or both.
 *
 * The host (WebControl/TemplateHierarchyInput) carries the stored nodes depth first
 * (data-value), the templates that may be added (data-candidates, each with the classes it
 * serves), the accounts a node may be assigned to (data-assignees) and the relation types a link
 * may name (data-links, each in both directions, read from the node, with the flag blocking).
 * The control shows the root template the wizard chose (the tile selection "Root" of the same
 * form) on top and draws one row per node, indented by its depth: drag handle, position, icon,
 * name, workspace, a badge naming the connection, and the buttons to indent, outdent, move and
 * remove it; below the head the class, the sub-object switch and the assignee, and the inner
 * list of links: per line a relation type, a multiple selection of the objects of the structure
 * (the root and the other nodes) and a button removing the line; a button below adds a line. A
 * row moves with its subtree; a removed row hands its children to its parent and leaves the
 * links naming it. The picker below the tree adds a template at the end, below the root;
 * indenting places it below the node above.
 *
 * The nodes are submitted through one hidden input as JSON (WebControl/TemplateHierarchyNodeList):
 * [{ id, parent, template, class, child, assignee, links: [{ link, targets }] }], depth first;
 * a target is the key of a row or "root". The pickers inside the rows are framework selections
 * without a name, so they post nothing themselves; their change events stay inside the control,
 * which announces one change of its own value.
 */
kleenestar.core.TemplateHierarchyCtrl = class extends webexpress.webui.Ctrl {
    /**
     * The prefix of the client translations.
     */
    static PREFIX = "kleenestar.core:template.hierarchy.";

    /**
     * The name of the root selection of the wizard.
     */
    static ROOT_FIELD = "Root";

    /**
     * The target of a link that names the object created from the root template.
     */
    static ROOT_TARGET = "root";

    /**
     * The counter behind the keys of rows added on this page.
     */
    static _nextKey = 0;

    /**
     * Constructor
     * @param {HTMLElement} element - The host element rendered by TemplateHierarchyInput.
     */
    constructor(element) {
        super(element);

        const id = element.getAttribute("id");
        const name = element.getAttribute("name") || "Nodes";

        this._candidates = this._parse(element.dataset.candidates, []);
        this._assignees = this._parse(element.dataset.assignees, []);
        this._links = this._parse(element.dataset.links, []);
        this._byId = new Map(this._candidates.map((x) => [String(x.id).toLowerCase(), x]));
        this._rows = [];
        this._dragKey = null;

        element.removeAttribute("id");
        element.removeAttribute("name");
        element.classList.add("ks-template-children", "ks-template-hierarchy");

        this._hidden = document.createElement("input");
        this._hidden.type = "hidden";
        this._hidden.name = name;
        if (id) {
            this._hidden.id = id;
        }

        this._root = this._createRoot();

        this._list = document.createElement("ol");
        this._list.className = "ks-template-children-list";
        this._list.setAttribute("role", "list");

        this._empty = this._createEmptyState();
        this._picker = this._createPicker();

        element.replaceChildren(this._hidden, this._root, this._list, this._empty, this._picker.host);

        // the selections inside report their own changes; the form hears one change of this
        // control instead, announced by _commit
        element.addEventListener(webexpress.webui.Event.CHANGE_VALUE_EVENT, (event) => {
            if (event.target !== element) {
                event.stopPropagation();
            }
        });

        // the root is chosen on an earlier step of the same form
        this._form = element.closest("form");
        this._onRootChange = () => this._renderRoot();
        this._form?.addEventListener("change", this._onRootChange);
        this._form?.addEventListener(webexpress.webui.Event.CHANGE_VALUE_EVENT, this._onRootChange);

        this._load(this._parse(element.dataset.value, []));
        this._renderRoot();

        element.removeAttribute("data-value");
        element.removeAttribute("data-candidates");
        element.removeAttribute("data-assignees");
        element.removeAttribute("data-links");
    }

    /**
     * Gets the nodes as they are submitted: the JSON array depth first.
     * @returns {string} The serialized nodes.
     */
    get value() {
        return this._serialize();
    }

    /**
     * Sets the nodes, from their JSON text or from the array.
     * @param {string|Array|null} value - The nodes.
     */
    set value(value) {
        const rows = Array.isArray(value) ? value : this._parse(value, []);
        this._load(rows);
    }

    /**
     * Validates the nodes: every node referencing a template that serves several classes names
     * the class its object is created in, every node is a sub-object, linked or both, every
     * link line names a relation type and at least one object, and the blocking links do not
     * run in a circle.
     * @returns {Array<string>} The messages, empty when the nodes are valid.
     */
    validate() {
        const messages = [];

        for (const row of this._rows) {
            const missingClass = this._needsClass(row) && !row.class;
            const unconnected = !this._isConnected(row);
            const incomplete = row.links.filter((x) => !this._isComplete(x));
            row.element.classList.toggle("ks-template-child-invalid", missingClass || unconnected || incomplete.length > 0);

            // the form moves the focus to the first element marked invalid, which brings the
            // row into view with the choice that is missing
            row.element.querySelector(".ks-template-child-class .wx-selection-trigger")?.setAttribute("aria-invalid", missingClass ? "true" : "false");
            row.linkAdd?.setAttribute("aria-invalid", unconnected && row.links.length === 0 ? "true" : "false");
            for (const link of row.links) {
                const broken = incomplete.includes(link);
                link.element.classList.toggle("ks-template-child-link-invalid", broken);
                link.typeHost.querySelector(".wx-selection-trigger")?.setAttribute("aria-invalid", broken && !link.link ? "true" : "false");
                link.targetHost.querySelector(".wx-selection-trigger")?.setAttribute("aria-invalid", broken && link.targets.length === 0 ? "true" : "false");
            }

            if (missingClass) {
                messages.push(this._t("class.required", "Choose the class for \"{0}\".").replace("{0}", () => this._nameOf(row)));
            }
            if (incomplete.length > 0) {
                messages.push(this._t("link.incomplete", "Each link of \"{0}\" needs a relation and at least one object.").replace("{0}", () => this._nameOf(row)));
            } else if (unconnected) {
                messages.push(this._t("connection.required", "Create \"{0}\" as a sub-object or add a link.").replace("{0}", () => this._nameOf(row)));
            }
        }

        if (this._hasBlockingCycle()) {
            messages.push(this._t("link.cycle", "The blocking links run in a circle; none of these objects could ever be finished."));
        }

        return messages;
    }

    /**
     * Removes the listeners when the control leaves the page.
     */
    destroy() {
        this._form?.removeEventListener("change", this._onRootChange);
        this._form?.removeEventListener(webexpress.webui.Event.CHANGE_VALUE_EVENT, this._onRootChange);
        this._rows = [];
        super.destroy();
    }

    /**
     * Replaces the rows with the given nodes; the depth follows from the parent of each node.
     * @param {Array<object>} entries - The nodes depth first.
     */
    _load(entries) {
        this._rows = [];
        this._list.replaceChildren();

        const depths = new Map();

        // a link may name a node further down, which does not exist yet while the rows are
        // built; the targets are offered, and checked, once every row is there (_render)
        this._loading = true;

        for (const entry of entries || []) {
            if (!entry || !entry.template) {
                continue;
            }

            const key = entry.id ? String(entry.id) : this._newKey();
            const parent = entry.parent ? String(entry.parent) : null;
            const depth = parent && depths.has(parent) ? depths.get(parent) + 1 : 0;
            depths.set(key, depth);

            this._rows.push(this._createRow({
                key: key,
                depth: depth,
                template: String(entry.template),
                class: entry.class ? String(entry.class) : null,
                child: entry.child !== false,
                assignee: entry.assignee ? String(entry.assignee) : null,
                links: (Array.isArray(entry.links) ? entry.links : [])
                    .filter((x) => x && (x.link || (x.targets || []).length > 0))
                    .map((x) => ({
                        link: x.link ? String(x.link) : null,
                        targets: (x.targets || []).map((t) => String(t))
                    }))
            }));
        }

        this._loading = false;
        this._render();
        this._sync(false);
    }

    /**
     * Adds a row for a template at the end, below the root; indenting places it below the node
     * above.
     * @param {string} templateId - The template.
     */
    _add(templateId) {
        const candidate = this._candidate(templateId);

        if (!candidate) {
            return;
        }

        const classes = candidate.classes || [];
        const row = this._createRow({
            key: this._newKey(),
            depth: 0,
            template: String(candidate.id),
            // a template serving one class needs no choice
            class: classes.length === 1 ? String(classes[0].id) : null,
            // a new node is a sub-object; links are added on the row
            child: true,
            assignee: null,
            links: []
        });

        this._rows.push(row);
        this._render();
        this._commit();

        row.element.scrollIntoView({ block: "nearest" });

        // a template serving several classes asks for its class right away
        const focus = this._needsClass(row)
            ? row.element.querySelector(".ks-template-child-class .wx-selection-trigger")
            : row.element.querySelector(".ks-template-child-remove");
        focus?.focus({ preventScroll: true });
    }

    /**
     * Removes a row; its children move up to its parent, and the links naming it name it no
     * longer.
     * @param {object} row - The row.
     */
    _remove(row) {
        const index = this._rows.indexOf(row);

        if (index < 0) {
            return;
        }

        const end = this._subtreeEnd(index);
        for (let i = index + 1; i < end; i++) {
            this._rows[i].depth -= 1;
        }

        this._rows.splice(index, 1);

        for (const other of this._rows) {
            for (const link of other.links) {
                link.targets = link.targets.filter((x) => x !== row.key);
            }
        }

        this._render();
        this._commit();

        // the focus stays in the tree: on the row that took the place, else on the picker
        const next = this._rows[Math.min(index, this._rows.length - 1)];
        const focus = next ? next.element.querySelector(".ks-template-child-remove") : this._picker.host.querySelector(".wx-selection-trigger");
        focus?.focus({ preventScroll: true });
    }

    /**
     * Moves a row with its subtree before its previous sibling or after its next sibling.
     * @param {object} row - The row.
     * @param {number} direction - -1 to move up, 1 to move down.
     * @param {string} [focusOf] - The selector of the button to keep the focus on.
     */
    _shift(row, direction, focusOf) {
        const index = this._rows.indexOf(row);
        const end = this._subtreeEnd(index);
        const block = this._rows.slice(index, end);

        if (direction < 0) {
            const previous = this._previousSibling(index);
            if (previous < 0) {
                return;
            }
            this._rows.splice(index, block.length);
            this._rows.splice(previous, 0, ...block);
        } else {
            if (end >= this._rows.length || this._rows[end].depth !== row.depth) {
                return;
            }
            const nextEnd = this._subtreeEnd(end);
            const next = this._rows.slice(end, nextEnd);
            this._rows.splice(index, block.length + next.length, ...next, ...block);
        }

        this._render();
        this._commit();
        this._keepFocus(row, focusOf);
    }

    /**
     * Changes the depth of a row with its subtree: indenting makes it a child of its previous
     * sibling, outdenting a sibling of its parent.
     * @param {object} row - The row.
     * @param {number} delta - 1 to indent, -1 to outdent.
     * @param {string} [focusOf] - The selector of the button to keep the focus on.
     */
    _indent(row, delta, focusOf) {
        const index = this._rows.indexOf(row);

        if (delta > 0 && this._previousSibling(index) < 0) {
            return;
        }
        if (delta < 0 && row.depth === 0) {
            return;
        }

        const end = this._subtreeEnd(index);
        for (let i = index; i < end; i++) {
            this._rows[i].depth += delta;
        }

        this._render();
        this._commit();
        this._keepFocus(row, focusOf);
    }

    /**
     * Moves a row with its subtree to the place of another row: before it, or after its
     * subtree, at its depth.
     * @param {object} row - The moved row.
     * @param {object} target - The row it is dropped on.
     * @param {boolean} before - True to drop before the target.
     */
    _moveTo(row, target, before) {
        const index = this._rows.indexOf(row);
        const end = this._subtreeEnd(index);
        const targetIndex = this._rows.indexOf(target);

        // a row cannot be dropped into its own subtree
        if (targetIndex >= index && targetIndex < end) {
            return;
        }

        const block = this._rows.splice(index, end - index);
        const delta = target.depth - row.depth;
        block.forEach((x) => { x.depth += delta; });

        const at = this._rows.indexOf(target);
        const insert = before ? at : this._subtreeEnd(at);
        this._rows.splice(insert, 0, ...block);

        this._render();
        this._commit();
    }

    /**
     * Returns the index after the subtree of a row.
     * @param {number} index - The index of the row.
     * @returns {number} The index of the first row outside its subtree.
     */
    _subtreeEnd(index) {
        const depth = this._rows[index].depth;
        let end = index + 1;
        while (end < this._rows.length && this._rows[end].depth > depth) {
            end++;
        }
        return end;
    }

    /**
     * Returns the index of the previous sibling of a row.
     * @param {number} index - The index of the row.
     * @returns {number} The index, or -1 when the row is the first of its parent.
     */
    _previousSibling(index) {
        const depth = this._rows[index].depth;
        for (let i = index - 1; i >= 0; i--) {
            if (this._rows[i].depth === depth) {
                return i;
            }
            if (this._rows[i].depth < depth) {
                return -1;
            }
        }
        return -1;
    }

    /**
     * Returns the parent row of a row.
     * @param {number} index - The index of the row.
     * @returns {object|null} The parent, or null below the root.
     */
    _parentOf(index) {
        const depth = this._rows[index].depth;
        for (let i = index - 1; i >= 0; i--) {
            if (this._rows[i].depth < depth) {
                return this._rows[i];
            }
        }
        return null;
    }

    /**
     * Draws the rows in their order: indentation, positions, buttons, the objects the links
     * may name and the empty state.
     */
    _render() {
        // a depth may not exceed the depth of the row before it by more than one
        this._rows.forEach((row, index) => {
            const limit = index === 0 ? 0 : this._rows[index - 1].depth + 1;
            row.depth = Math.max(0, Math.min(row.depth, limit));
        });

        this._list.replaceChildren(...this._rows.map((row) => row.element));

        const counters = [];
        this._rows.forEach((row, index) => {
            counters.length = row.depth + 1;
            counters[row.depth] = (counters[row.depth] || 0) + 1;

            row.element.style.setProperty("--ks-depth", String(row.depth));
            row.element.dataset.depth = String(row.depth);
            row.position.textContent = counters.slice(0, row.depth + 1).map((x) => x || 1).join(".");
            row.up.disabled = this._previousSibling(index) < 0;
            const end = this._subtreeEnd(index);
            row.down.disabled = end >= this._rows.length || this._rows[end].depth !== row.depth;
            row.indent.disabled = this._previousSibling(index) < 0;
            row.outdent.disabled = row.depth === 0;
            row.parentName.textContent = this._parentOf(index) ? this._nameOf(this._parentOf(index)) : this._rootName();
        });

        // the targets name the positions, which are known only now
        this._rows.forEach((row) => this._renderLinkTargets(row));

        this._empty.hidden = this._rows.length > 0;
        this._list.hidden = this._rows.length === 0;
    }

    /**
     * Writes the hidden input and announces the change of the nodes.
     */
    _commit() {
        this._sync(true);

        // a row that was invalid may be valid now; the marker follows the tree, and the badge
        // of the head names the connection as it is
        for (const row of this._rows) {
            for (const link of row.links) {
                if (link.element.classList.contains("ks-template-child-link-invalid") && this._isComplete(link)) {
                    link.element.classList.remove("ks-template-child-link-invalid");
                    link.element.querySelectorAll(".wx-selection-trigger[aria-invalid]").forEach((x) => x.setAttribute("aria-invalid", "false"));
                }
            }
            if (row.element.classList.contains("ks-template-child-invalid") && (!this._needsClass(row) || row.class) && this._isConnected(row) && row.links.every((x) => this._isComplete(x))) {
                row.element.classList.remove("ks-template-child-invalid");
                row.element.querySelectorAll("[aria-invalid]").forEach((x) => x.setAttribute("aria-invalid", "false"));
            }
            this._renderConnection(row);
        }

        this._dispatch(webexpress.webui.Event.CHANGE_VALUE_EVENT, { value: this.value });
    }

    /**
     * Writes the nodes into the hidden input.
     * @param {boolean} notify - True to fire the change event of the input, as a user change does.
     */
    _sync(notify) {
        const value = this._serialize();

        if (this._hidden.value === value) {
            return;
        }

        this._hidden.value = value;

        if (notify) {
            this._hidden.dispatchEvent(new Event("change", { bubbles: true }));
        }
    }

    /**
     * Serializes the rows depth first, each naming its parent.
     * @returns {string} The JSON array.
     */
    _serialize() {
        return JSON.stringify(this._rows.map((row, index) => ({
            id: row.key,
            parent: this._parentOf(index)?.key || null,
            template: row.template,
            class: row.class || null,
            child: row.child !== false,
            assignee: row.assignee || null,
            links: row.links
                .filter((x) => x.link || x.targets.length > 0)
                .map((x) => ({ link: x.link || null, targets: [...x.targets] }))
        })));
    }

    /**
     * Creates the card of the root template the wizard chose; its content follows the root
     * selection (_renderRoot).
     * @returns {HTMLElement} The card.
     */
    _createRoot() {
        const root = document.createElement("div");
        root.className = "ks-template-hierarchy-root";
        return root;
    }

    /**
     * Shows the root template of the root selection, and names it as the parent of the rows
     * below the root.
     */
    _renderRoot() {
        const candidate = this._rootCandidate();
        const root = this._root;
        root.replaceChildren();

        const icon = candidate && candidate.icon ? Object.assign(document.createElement("img"), { className: "ks-template-child-icon", src: candidate.icon, alt: "" }) : this._icon("sitemap");
        const label = document.createElement("span");
        label.className = "ks-template-child-label";
        label.textContent = this._t("root", "Root");
        const name = document.createElement("span");
        name.className = "ks-template-child-name";
        name.textContent = candidate ? candidate.name : this._t("root.none", "Choose the root template on the previous step");

        root.append(icon, label, name);
        root.classList.toggle("ks-template-hierarchy-root-none", !candidate);

        for (const row of this._rows) {
            if (row.parentName && row.depth === 0) {
                row.parentName.textContent = this._rootName();
            }
        }
    }

    /**
     * Returns the root template the form names.
     * @returns {object|null} The template, or null.
     */
    _rootCandidate() {
        const input = this._form?.querySelector(`[name="${kleenestar.core.TemplateHierarchyCtrl.ROOT_FIELD}"]`);
        const value = String(input?.value || "").split(";")[0];
        return this._candidate(value.split("@")[0]);
    }

    /**
     * Returns the name of the root template.
     * @returns {string} The name, or the word for the root.
     */
    _rootName() {
        return this._rootCandidate()?.name || this._t("root", "Root");
    }

    /**
     * Creates the row of a node.
     * @param {object} entry - The node: key, depth, template, class, child, assignee, links.
     * @returns {object} The row: the node with its elements and selections.
     */
    _createRow(entry) {
        const row = Object.assign({}, entry);
        row.links = [];
        const candidate = this._candidate(row.template);
        const element = document.createElement("li");
        element.className = "ks-template-child";
        element.dataset.key = row.key;
        row.element = element;

        // the head: handle, position, icon, name with workspace, connection and state, actions
        const head = document.createElement("div");
        head.className = "ks-template-child-head";

        const handle = document.createElement("button");
        handle.type = "button";
        handle.className = "ks-template-child-handle";
        handle.draggable = true;
        handle.title = this._t("drag", "Drag to move the node with its subtree; Alt+Arrow keys move and indent it");
        handle.setAttribute("aria-label", handle.title);
        handle.appendChild(this._icon("grip-vertical"));
        head.appendChild(handle);

        row.position = document.createElement("span");
        row.position.className = "ks-template-child-position";
        head.appendChild(row.position);

        if (candidate && candidate.icon) {
            const icon = document.createElement("img");
            icon.className = "ks-template-child-icon";
            icon.src = candidate.icon;
            icon.alt = "";
            head.appendChild(icon);
        }

        const title = document.createElement("div");
        title.className = "ks-template-child-title";

        const name = document.createElement("span");
        name.className = "ks-template-child-name";
        name.textContent = this._nameOf(row);
        title.appendChild(name);

        if (candidate && !candidate.local && candidate.workspace) {
            title.appendChild(this._badge(candidate.workspace, "ks-template-child-workspace", this._t("workspace", "Created in the workspace {0}").replace("{0}", () => candidate.workspace)));
        }
        if (!candidate) {
            title.appendChild(this._badge(this._t("missing", "Not available"), "ks-template-child-archived", this._t("missing.help", "The template is not available to you.")));
        } else if (candidate.archived) {
            title.appendChild(this._badge(this._t("archived", "Archived"), "ks-template-child-archived", this._t("archived.help", "An archived template creates no object.")));
        }

        // how the object is connected, and to which parent
        row.connection = document.createElement("span");
        row.connection.className = "ks-template-child-connection";
        title.appendChild(row.connection);

        const meta = document.createElement("span");
        meta.className = "ks-template-child-meta";
        const under = document.createElement("span");
        under.textContent = this._t("under", "below") + " ";
        row.parentName = document.createElement("strong");
        meta.append(under, row.parentName);
        const description = candidate && (candidate.description || (candidate.category ? String(candidate.category).split(";").join(" · ") : ""));
        if (description) {
            meta.append(document.createTextNode(" · " + description));
        }
        title.appendChild(meta);

        head.appendChild(title);

        const actions = document.createElement("div");
        actions.className = "ks-template-child-actions";
        row.indent = this._button("indent", this._t("indent", "Indent (make it a child of the node above)"), "ks-template-child-indent", () => this._indent(row, 1, ".ks-template-child-indent"));
        row.outdent = this._button("outdent", this._t("outdent", "Outdent"), "ks-template-child-outdent", () => this._indent(row, -1, ".ks-template-child-outdent"));
        row.up = this._button("arrow-up", this._t("moveup", "Move up"), "ks-template-child-up", () => this._shift(row, -1, ".ks-template-child-up"));
        row.down = this._button("arrow-down", this._t("movedown", "Move down"), "ks-template-child-down", () => this._shift(row, 1, ".ks-template-child-down"));
        const remove = this._button("xmark", this._t("remove", "Remove (its nodes move up)"), "ks-template-child-remove", () => this._remove(row));
        actions.append(row.outdent, row.indent, row.up, row.down, remove);
        head.appendChild(actions);

        element.appendChild(head);

        // the settings of the node: class, sub-object, assignee, and the links below them
        const fields = document.createElement("div");
        fields.className = "ks-template-child-fields";
        fields.appendChild(this._createClassField(row, candidate));
        fields.appendChild(this._createConnectionField(row));
        fields.appendChild(this._createAssigneeField(row));
        fields.appendChild(this._createLinksField(row, entry.links || []));
        element.appendChild(fields);
        this._renderConnection(row);

        this._attachDrag(row, handle);

        handle.addEventListener("keydown", (event) => {
            if (!event.altKey) {
                return;
            }
            const actions = {
                ArrowUp: () => this._shift(row, -1, ".ks-template-child-handle"),
                ArrowDown: () => this._shift(row, 1, ".ks-template-child-handle"),
                ArrowRight: () => this._indent(row, 1, ".ks-template-child-handle"),
                ArrowLeft: () => this._indent(row, -1, ".ks-template-child-handle")
            };
            if (actions[event.key]) {
                event.preventDefault();
                actions[event.key]();
            }
        });

        return row;
    }

    /**
     * Creates the class field of a row: a choice where the template serves several classes,
     * the class itself where it serves one.
     * @param {object} row - The row.
     * @param {object|null} candidate - The referenced template.
     * @returns {HTMLElement} The field.
     */
    _createClassField(row, candidate) {
        const classes = (candidate && candidate.classes) || [];
        const field = this._field(this._t("class", "Class"), "ks-template-child-class");

        if (classes.length <= 1) {
            const value = document.createElement("span");
            value.className = "ks-template-child-static";
            const only = classes[0];
            if (only && only.icon) {
                const icon = document.createElement("img");
                icon.className = "wx-icon";
                icon.src = only.icon;
                icon.alt = "";
                value.appendChild(icon);
            }
            value.appendChild(document.createTextNode(only ? only.name : "–"));
            field.appendChild(value);
            return field;
        }

        // the stored choice only counts while the template still serves the class
        if (row.class && !classes.some((x) => this._same(x.id, row.class))) {
            row.class = null;
        }

        this._selection(field, {
            placeholder: this._t("class.placeholder", "Choose a class"),
            sticky: true,
            options: classes.map((x) => ({
                id: String(x.id),
                label: x.name,
                content: this._esc(x.name),
                image: x.icon || null
            })),
            value: row.class,
            onChange: (value) => {
                row.class = value;
                this._commit();
            }
        });

        field.classList.add("ks-template-child-choice");
        return field;
    }

    /**
     * Creates the connection field of a row: a switch that creates the object as a sub-object
     * of its parent.
     * @param {object} row - The row.
     * @returns {HTMLElement} The field.
     */
    _createConnectionField(row) {
        const field = this._field(this._t("connection", "Connection"), "ks-template-child-connect");
        const body = document.createElement("div");
        body.className = "ks-template-child-connect-body";

        const id = `ks-template-child-sub-${row.key}`;
        const check = document.createElement("div");
        check.className = "form-check form-switch ks-template-child-sub";
        const input = document.createElement("input");
        input.type = "checkbox";
        input.className = "form-check-input";
        input.id = id;
        input.checked = row.child !== false;
        input.setAttribute("role", "switch");
        const label = document.createElement("label");
        label.className = "form-check-label";
        label.htmlFor = id;
        label.textContent = this._t("child", "Sub-object");
        label.title = this._t("child.help", "The object is created below the object of its parent node and counts towards its progress.");
        check.append(input, label);

        input.addEventListener("change", (event) => {
            event.stopPropagation();
            row.child = input.checked;
            this._commit();
        });

        body.appendChild(check);
        field.appendChild(body);

        return field;
    }

    /**
     * Creates the links field of a row: the inner list of links, one line per link with the
     * relation type, the objects it joins and a button removing the line, and a button adding
     * a line.
     * @param {object} row - The row.
     * @param {Array<object>} links - The stored links: link, targets.
     * @returns {HTMLElement} The field.
     */
    _createLinksField(row, links) {
        const field = this._field(this._t("links", "Links"), "ks-template-child-links");

        row.linkList = document.createElement("ul");
        row.linkList.className = "ks-template-child-link-list";
        // a list with a role is left alone by the content styles of plain lists
        row.linkList.setAttribute("role", "list");
        field.appendChild(row.linkList);

        const add = document.createElement("button");
        add.type = "button";
        add.className = "btn btn-sm btn-link ks-template-child-link-add";
        add.append(this._icon("plus"), document.createTextNode(" " + this._t("link.add", "Add link")));
        add.addEventListener("click", (event) => {
            event.preventDefault();
            const link = this._addLink(row, { link: null, targets: [] });
            this._commit();
            link.typeHost.querySelector(".wx-selection-trigger")?.focus({ preventScroll: true });
        });
        row.linkAdd = add;
        field.appendChild(add);

        for (const link of links) {
            this._addLink(row, link);
        }

        return field;
    }

    /**
     * Adds a line to the links of a row.
     * @param {object} row - The row.
     * @param {object} entry - The link: link (relation type key, ":inverse" for the other
     * direction), targets (row keys or "root").
     * @returns {object} The link with its elements.
     */
    _addLink(row, entry) {
        // a relation type the catalog no longer offers is dropped rather than shown as a key
        const link = {
            link: entry.link && this._links.some((x) => x.value === entry.link) ? entry.link : null,
            targets: [...(entry.targets || [])]
        };

        link.element = document.createElement("li");
        link.element.className = "ks-template-child-link-row";

        link.typeHost = document.createElement("div");
        link.typeHost.className = "ks-template-child-link-type";
        link.targetHost = document.createElement("div");
        link.targetHost.className = "ks-template-child-link-targets";

        this._selection(link.typeHost, {
            placeholder: this._t("link.placeholder", "Choose a relation"),
            sticky: true,
            options: this._links.map((x) => ({
                id: String(x.value),
                label: x.label,
                content: this._esc(x.label),
                icon: x.icon ? this._iconClass(x.icon) : null
            })),
            value: link.link,
            onChange: (value) => {
                link.link = value;
                this._commit();
            }
        });

        link.targetSelection = this._selection(link.targetHost, {
            placeholder: this._t("link.targets.placeholder", "Choose objects"),
            sticky: false,
            multiple: true,
            options: [],
            value: null,
            onChange: (values) => {
                link.targets = values;
                this._commit();
            }
        });

        const remove = this._button("trash", this._t("link.remove", "Remove link"), "ks-template-child-link-remove", () => {
            row.links = row.links.filter((x) => x !== link);
            link.element.remove();
            this._commit();
            row.linkAdd?.focus({ preventScroll: true });
        });

        link.element.append(link.typeHost, link.targetHost, remove);
        row.linkList.appendChild(link.element);
        row.links.push(link);

        this._renderLinkTargets(row);

        return link;
    }

    /**
     * Offers every place of the structure but the row itself to the links of a row: the root
     * and the other nodes, each named with its position. Targets naming a place that no longer
     * exists are dropped.
     * @param {object} row - The row.
     */
    _renderLinkTargets(row) {
        if (this._loading || !row.links || row.links.length === 0 || !row.position) {
            return;
        }

        // the chosen objects show as info chips, apart from the relation beside them
        const rootTarget = kleenestar.core.TemplateHierarchyCtrl.ROOT_TARGET;
        const color = "wx-selection-info";
        const options = [{
            id: rootTarget,
            label: this._rootName(),
            color: color,
            content: `<span class="ks-template-child-option-position">${this._esc(this._t("root", "Root"))}</span> ${this._esc(this._rootName())}`
        }].concat(this._rows
            .filter((other) => other !== row)
            .map((other) => ({
                id: other.key,
                label: `${other.position.textContent} ${this._nameOf(other)}`,
                color: color,
                content: `<span class="ks-template-child-option-position">${this._esc(other.position.textContent)}</span> ${this._esc(this._nameOf(other))}`
            })));

        for (const link of row.links) {
            link.targets = link.targets.filter((x) => options.some((o) => o.id === x));
            link.targetSelection._silent = true;
            link.targetSelection.options = options;
            link.targetSelection.value = [...link.targets];
            link.targetSelection._silent = false;
        }
    }

    /**
     * Tells whether a link line is complete: it names a relation type and at least one object.
     * @param {object} link - The link.
     * @returns {boolean} True when the line is complete.
     */
    _isComplete(link) {
        return !!link.link && link.targets.length > 0;
    }

    /**
     * Tells whether the blocking links close a circle. A blocking link without inverse runs
     * from its row to its targets (the row blocks them), with inverse the other way round.
     * @returns {boolean} True when an object would be blocked by itself.
     */
    _hasBlockingCycle() {
        const blocking = new Set(this._links.filter((x) => x.blocking).map((x) => x.value));
        const edges = new Map();
        const add = (from, to) => {
            if (!edges.has(from)) {
                edges.set(from, new Set());
            }
            edges.get(from).add(to);
        };

        for (const row of this._rows) {
            for (const link of row.links) {
                if (!link.link || !blocking.has(link.link)) {
                    continue;
                }
                const inverse = link.link.endsWith(":inverse");
                for (const target of link.targets) {
                    if (inverse) {
                        add(target, row.key);
                    } else {
                        add(row.key, target);
                    }
                }
            }
        }

        const state = new Map();
        const visit = (place) => {
            state.set(place, 1);
            for (const next of edges.get(place) || []) {
                const seen = state.get(next) || 0;
                if (seen === 1 || (seen === 0 && visit(next))) {
                    return true;
                }
            }
            state.set(place, 2);
            return false;
        };

        return [...edges.keys()].some((place) => !state.has(place) && visit(place));
    }

    /**
     * Writes the badge of the head that names how the object of a row is connected.
     * @param {object} row - The row.
     */
    _renderConnection(row) {
        if (!row.connection) {
            return;
        }

        const parts = [];
        if (row.child !== false) {
            parts.push(this._t("child", "Sub-object"));
        }
        const labels = new Set(row.links
            .filter((x) => this._isComplete(x))
            .map((x) => this._links.find((l) => l.value === x.link)?.label)
            .filter(Boolean));
        parts.push(...labels);

        row.connection.textContent = parts.length > 0 ? parts.join(" · ") : this._t("connection.none", "Not connected");
        row.connection.classList.toggle("ks-template-child-connection-none", parts.length === 0);
    }

    /**
     * Tells whether a row is a sub-object, linked or both.
     * @param {object} row - The row.
     * @returns {boolean} True when the object is connected to the structure.
     */
    _isConnected(row) {
        return row.child !== false || row.links.some((x) => this._isComplete(x));
    }

    /**
     * Creates the assignee field of a row.
     * @param {object} row - The row.
     * @returns {HTMLElement} The field.
     */
    _createAssigneeField(row) {
        const field = this._field(this._t("assignee", "Assignee"), "ks-template-child-assignee");

        this._selection(field, {
            placeholder: this._t("assignee.placeholder", "Unassigned"),
            sticky: false,
            options: this._assignees.map((x) => ({
                id: String(x.id),
                label: x.name,
                content: this._esc(x.name) + (x.position ? ` <small class="ks-template-child-option-meta">${this._esc(x.position)}</small>` : ""),
                image: x.avatar || null
            })),
            value: row.assignee,
            onChange: (value) => {
                row.assignee = value;
                this._commit();
            }
        });

        return field;
    }

    /**
     * Creates the picker that adds a template to the tree: a framework selection over the
     * templates that may be added, grouped by workspace, searchable by name.
     * @returns {object} The picker: its host element and its selection.
     */
    _createPicker() {
        const host = document.createElement("div");
        host.className = "ks-template-children-add";

        const offered = this._candidates.filter((x) => x.offered);
        const options = [];
        let group = null;

        for (const candidate of offered) {
            const label = candidate.local ? this._t("group.local", "This workspace") : (candidate.workspace || "");
            if (label !== group) {
                options.push({ type: "header", content: this._esc(label) });
                group = label;
            }

            const classes = (candidate.classes || []).map((x) => x.name).join(", ");
            const meta = [classes, candidate.category ? String(candidate.category).split(";").join(" · ") : null].filter(Boolean).join(" · ");

            options.push({
                id: String(candidate.id),
                label: candidate.name,
                content: `<span class="ks-template-child-option"><span>${this._esc(candidate.name)}</span>${meta ? `<small class="ks-template-child-option-meta">${this._esc(meta)}</small>` : ""}</span>`,
                image: candidate.icon || null
            });
        }

        const selection = this._selection(host, {
            placeholder: offered.length > 0
                ? this._t("add", "Add a template")
                : this._t("add.none", "No template can be added"),
            sticky: false,
            options: options,
            value: null,
            onChange: (value) => {
                if (!value) {
                    return;
                }
                // the picker is a command, not a value: it adds the row and empties again
                selection._silent = true;
                selection.value = [];
                selection._silent = false;
                this._add(value);
            }
        });

        // the picker is a command: a plus in front of its placeholder says so
        host.querySelector(".wx-selection > .form-control")?.prepend(Object.assign(this._icon("plus"), {
            className: `${this._iconClass("plus")} ks-template-children-add-icon`
        }));

        return { host, selection };
    }

    /**
     * Creates the empty state shown while the tree holds no node.
     * @returns {HTMLElement} The empty state.
     */
    _createEmptyState() {
        const empty = document.createElement("div");
        empty.className = "wx-empty-state ks-template-children-empty";

        const icon = document.createElement("div");
        icon.className = "wx-empty-state-icon";
        icon.appendChild(this._icon("sitemap"));

        const title = document.createElement("div");
        title.className = "wx-empty-state-title";
        title.textContent = this._t("empty.title", "No nodes yet");

        const message = document.createElement("div");
        message.className = "wx-empty-state-message";
        message.textContent = this._t("empty.message", "Add existing templates below. Each one creates its object with the root object: as a sub-object, linked or both. Indent a node to place it below another.");

        empty.append(icon, title, message);
        return empty;
    }

    /**
     * Creates a framework selection inside a container. The selection carries no name, so it
     * posts nothing; its value is reported through onChange: the chosen id or null, or with
     * multiple the array of the chosen ids.
     * @param {HTMLElement} container - The element the selection is appended to.
     * @param {object} config - placeholder, sticky, multiple, options, value, onChange.
     * @returns {webexpress.webui.InputSelectionCtrl} The selection.
     */
    _selection(container, config) {
        const host = document.createElement("div");
        host.setAttribute("placeholder", config.placeholder || "");
        if (config.sticky) {
            host.dataset.stickySelection = "true";
        }
        if (config.multiple) {
            host.dataset.multiselection = "true";
        }
        container.appendChild(host);

        const selection = new webexpress.webui.InputSelectionCtrl(host);

        selection._silent = true;
        selection.options = config.options || [];
        selection.value = config.value ? [String(config.value)] : [];
        selection._silent = false;

        host.addEventListener(webexpress.webui.Event.CHANGE_VALUE_EVENT, (event) => {
            if (event.target !== host || selection._silent) {
                return;
            }
            const values = ((event.detail && event.detail.value) || []).map((x) => String(x));
            config.onChange(config.multiple ? values : (values.length > 0 ? values[0] : null));
        });

        return selection;
    }

    /**
     * Lets a row be dragged by its handle onto another row: dropped on the upper half it is
     * placed before that row, on the lower half after its subtree, at its depth.
     * @param {object} row - The row.
     * @param {HTMLElement} handle - The drag handle.
     */
    _attachDrag(row, handle) {
        const element = row.element;
        const clear = () => this._rows.forEach((other) => other.element.classList.remove("ks-template-child-drop-before", "ks-template-child-drop-after"));

        handle.addEventListener("dragstart", (event) => {
            this._dragKey = row.key;
            element.classList.add("ks-template-child-dragging");
            event.dataTransfer.effectAllowed = "move";
            event.dataTransfer.setData("text/plain", row.key);
            event.dataTransfer.setDragImage(element, 16, 16);
        });

        handle.addEventListener("dragend", () => {
            this._dragKey = null;
            element.classList.remove("ks-template-child-dragging");
            clear();
        });

        element.addEventListener("dragover", (event) => {
            const dragged = this._rows.find((x) => x.key === this._dragKey);
            if (!dragged || dragged === row || this._inSubtree(dragged, row)) {
                return;
            }
            event.preventDefault();
            event.stopPropagation();
            event.dataTransfer.dropEffect = "move";
            const before = this._isUpperHalf(element, event);
            element.classList.toggle("ks-template-child-drop-before", before);
            element.classList.toggle("ks-template-child-drop-after", !before);
        });

        element.addEventListener("dragleave", (event) => {
            if (!element.contains(event.relatedTarget)) {
                element.classList.remove("ks-template-child-drop-before", "ks-template-child-drop-after");
            }
        });

        element.addEventListener("drop", (event) => {
            const dragged = this._rows.find((x) => x.key === this._dragKey);
            clear();

            if (!dragged || dragged === row || this._inSubtree(dragged, row)) {
                return;
            }

            event.preventDefault();
            event.stopPropagation();
            this._moveTo(dragged, row, this._isUpperHalf(element, event));
        });
    }

    /**
     * Tells whether a row lies in the subtree of another.
     * @param {object} root - The root of the subtree.
     * @param {object} row - The row.
     * @returns {boolean} True when the row is below the root.
     */
    _inSubtree(root, row) {
        const index = this._rows.indexOf(root);
        const at = this._rows.indexOf(row);
        return at > index && at < this._subtreeEnd(index);
    }

    /**
     * Tells whether a pointer event lies in the upper half of the head of a row.
     * @param {HTMLElement} element - The row element.
     * @param {DragEvent} event - The event.
     * @returns {boolean} True for the upper half.
     */
    _isUpperHalf(element, event) {
        const rect = element.getBoundingClientRect();
        return event.clientY < rect.top + rect.height / 2;
    }

    /**
     * Keeps the focus on a button of a row after the tree was drawn anew.
     * @param {object} row - The row.
     * @param {string} [selector] - The selector of the button.
     */
    _keepFocus(row, selector) {
        if (!selector) {
            return;
        }
        const button = row.element.querySelector(selector);
        (button && !button.disabled ? button : row.element.querySelector(".ks-template-child-handle"))?.focus({ preventScroll: true });
    }

    /**
     * Tells whether a row has to name its class: its template serves several classes.
     * @param {object} row - The row.
     * @returns {boolean} True when a class has to be chosen.
     */
    _needsClass(row) {
        const candidate = this._candidate(row.template);
        return !!candidate && (candidate.classes || []).length > 1;
    }

    /**
     * Returns the name a row is shown with.
     * @param {object} row - The row.
     * @returns {string} The name of the referenced template.
     */
    _nameOf(row) {
        const candidate = this._candidate(row.template);
        return candidate ? candidate.name : this._t("missing", "Not available");
    }

    /**
     * Returns a template the host describes.
     * @param {string} id - The template id.
     * @returns {object|null} The template, or null.
     */
    _candidate(id) {
        return id ? this._byId.get(String(id).toLowerCase()) || null : null;
    }

    /**
     * Creates a labelled field of a row.
     * @param {string} label - The label.
     * @param {string} css - The class of the field.
     * @returns {HTMLElement} The field.
     */
    _field(label, css) {
        const field = document.createElement("div");
        field.className = `ks-template-child-field ${css}`;
        const caption = document.createElement("span");
        caption.className = "ks-template-child-label";
        caption.textContent = label;
        field.appendChild(caption);
        return field;
    }

    /**
     * Creates an icon button of a row.
     * @param {string} icon - The icon name.
     * @param {string} label - The accessible label and tooltip.
     * @param {string} css - The class of the button.
     * @param {function(): void} action - What a click does.
     * @returns {HTMLButtonElement} The button.
     */
    _button(icon, label, css, action) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = `btn btn-sm btn-link ${css}`;
        button.title = label;
        button.setAttribute("aria-label", label);
        button.appendChild(this._icon(icon));
        button.addEventListener("click", (event) => {
            event.preventDefault();
            action();
        });
        return button;
    }

    /**
     * Creates a badge of a row title.
     * @param {string} text - The text.
     * @param {string} css - The class of the badge.
     * @param {string} [title] - The tooltip.
     * @returns {HTMLElement} The badge.
     */
    _badge(text, css, title) {
        const badge = document.createElement("span");
        badge.className = `ks-template-child-badge ${css}`;
        badge.textContent = text;
        if (title) {
            badge.title = title;
        }
        return badge;
    }

    /**
     * Creates an icon of the active icon set.
     * @param {string} name - The symbolic icon name.
     * @returns {HTMLElement} The icon.
     */
    _icon(name) {
        const icon = document.createElement("i");
        icon.className = this._iconClass(name);
        icon.setAttribute("aria-hidden", "true");
        return icon;
    }

    /**
     * Compares two ids ignoring case.
     * @param {string} a - The first id.
     * @param {string} b - The second id.
     * @returns {boolean} True when both name the same record.
     */
    _same(a, b) {
        return String(a || "").toLowerCase() === String(b || "").toLowerCase();
    }

    /**
     * Creates a key for a row added on this page.
     * @returns {string} The key.
     */
    _newKey() {
        kleenestar.core.TemplateHierarchyCtrl._nextKey += 1;
        return `new-${Date.now().toString(36)}-${kleenestar.core.TemplateHierarchyCtrl._nextKey}`;
    }

    /**
     * Translates a key of the control.
     * @param {string} key - The key below the prefix.
     * @param {string} fallback - The text without translation.
     * @returns {string} The text.
     */
    _t(key, fallback) {
        return this._i18n(kleenestar.core.TemplateHierarchyCtrl.PREFIX + key, fallback);
    }

    /**
     * Escapes a text for markup.
     * @param {string} text - The text.
     * @returns {string} The escaped text.
     */
    _esc(text) {
        return String(text ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    /**
     * Parses JSON text, answering a fallback for text that is no JSON.
     * @param {string} text - The text.
     * @param {*} fallback - The answer for missing or broken text.
     * @returns {*} The parsed value.
     */
    _parse(text, fallback) {
        if (!text) {
            return fallback;
        }
        try {
            const value = JSON.parse(text);
            return value ?? fallback;
        } catch (error) {
            console.warn("kleenestar.core.TemplateHierarchyCtrl:", error);
            return fallback;
        }
    }
};

// register the class in the controller
webexpress.webui.Controller.registerClass("wx-kleenestar-templatehierarchy", kleenestar.core.TemplateHierarchyCtrl);
