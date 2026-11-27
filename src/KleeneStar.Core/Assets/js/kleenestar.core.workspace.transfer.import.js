var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * The import tab of the workspace transfer settings page: `wx-kleenestar-transferimport` on the
 * host WorkspaceTransferImportControl renders (.ks-transfer-import), carrying the address of the
 * transfer endpoint (data-uri) and the translated words of the report (data-*).
 *
 * The export file is dropped on the zone or chosen through it. Nothing runs until a button is
 * pressed: "Check" reads the file without writing, "Import" writes it. The framework forms post
 * no file, so the archive is sent as the body of the POST that starts a background job
 * (kleenestar.core.WorkspaceTransferJobs); the panel shows its progress and the report follows.
 * With overwriting switched on, the import asks first - naming the workspaces it overwrites when
 * the same file was checked with the same choice before.
 */
kleenestar.core.WorkspaceTransferImportCtrl = class extends webexpress.webui.Ctrl {
    /**
     * Constructor
     * @param {HTMLElement} element - The DOM element associated with the instance.
     */
    constructor(element) {
        super(element);

        this._uri = element.dataset.uri || "";
        this._words = element.dataset;
        this._drop = element.querySelector(".ks-transfer-drop");
        this._input = element.querySelector(".ks-transfer-file");
        this._overwrite = element.querySelector(".ks-transfer-overwrite");
        this._check = element.querySelector(".ks-transfer-check");
        this._run = element.querySelector(".ks-transfer-run");
        this._panel = element.querySelector(".ks-transfer-job");
        this._result = element.querySelector(".ks-transfer-result");
        this._file = null;
        this._checked = null;
        this._busy = false;

        this._input.addEventListener("change", () => this._choose(this._input.files?.[0]));
        this._drop.addEventListener("keydown", (event) => {
            if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                this._input.click();
            }
        });

        for (const name of ["dragenter", "dragover"]) {
            this._drop.addEventListener(name, (event) => {
                event.preventDefault();
                this._drop.classList.add("ks-transfer-drop-over");
            });
        }

        for (const name of ["dragleave", "drop"]) {
            this._drop.addEventListener(name, (event) => {
                event.preventDefault();
                this._drop.classList.remove("ks-transfer-drop-over");
            });
        }

        this._drop.addEventListener("drop", (event) => this._choose(event.dataTransfer?.files?.[0]));
        // a check answers for the choice it was made with
        this._overwrite.addEventListener("change", () => {
            this._checked = null;
        });
        this._check.addEventListener("click", () => this._start(true));
        this._run.addEventListener("click", () => this._import());

        this._resume();
    }

    /**
     * Takes a chosen file; nothing runs until a button is pressed.
     * @param {File} file - The file.
     */
    _choose(file) {
        if (!file) {
            return;
        }

        if (!/\.zip$/i.test(file.name) && file.type && !/zip/i.test(file.type)) {
            this._message(this._words.errorNotzip, "danger");
            return;
        }

        this._file = file;
        this._checked = null;
        this._drop.classList.add("ks-transfer-drop-chosen");
        this._drop.querySelector(".ks-transfer-drop-file").hidden = false;
        this._drop.querySelector(".ks-transfer-drop-name").textContent = file.name;
        this._drop.querySelector(".ks-transfer-drop-size").textContent = kleenestar.core.WorkspaceTransferJobs.size(file.size);
        this._result.replaceChildren();
        this._panel.hidden = true;
        this._enable();
    }

    /**
     * Imports the chosen file - after a confirmation when it may overwrite workspaces.
     */
    async _import() {
        if (!this._file || this._busy) {
            return;
        }

        if (!this._overwrite.checked) {
            await this._start(false);
            return;
        }

        // a check of the same file and choice names what is overwritten; without one the
        // question stays general rather than running a check nobody asked for
        if (this._checked) {
            const replaced = (this._checked.workspaces || []).filter((x) => x.imported && x.replaced);

            if (replaced.length === 0) {
                await this._start(false);
                return;
            }

            this._confirm((this._words.confirmText || "{0}").replace("{0}", replaced.map((x) => `${x.name} (${x.key})`).join(", ")));
            return;
        }

        this._confirm(this._words.confirmGeneric || "");
    }

    /**
     * Asks before overwriting workspaces.
     * @param {string} question - What the import would do.
     */
    _confirm(question) {
        this._result.querySelector(".ks-transfer-confirm")?.remove();

        const bar = document.createElement("div");
        const text = document.createElement("div");
        const actions = document.createElement("div");
        const yes = document.createElement("button");
        const no = document.createElement("button");

        bar.className = "ks-transfer-confirm";
        text.className = "ks-transfer-confirm-text";
        text.textContent = question;
        actions.className = "ks-transfer-confirm-actions";
        yes.type = "button";
        yes.className = "btn btn-danger btn-sm";
        yes.textContent = this._words.confirmButton || "OK";
        no.type = "button";
        no.className = "btn btn-outline-secondary btn-sm";
        no.textContent = this._words.confirmCancel || "Cancel";

        yes.addEventListener("click", () => {
            bar.remove();
            this._start(false);
        });
        no.addEventListener("click", () => bar.remove());

        actions.append(yes, no);
        bar.append(text, actions);
        this._result.prepend(bar);
        yes.focus();
    }

    /**
     * Starts a job for the chosen file and follows it.
     * @param {boolean} dryRun - Whether the file is only checked.
     * @returns {Promise<object|null>} The report.
     */
    async _start(dryRun) {
        if (!this._file || this._busy) {
            return null;
        }

        this._busy = true;
        this._enable();
        this._result.replaceChildren();
        kleenestar.core.WorkspaceTransferJobs.fail(this._panel, "");

        try {
            const job = await kleenestar.core.WorkspaceTransferJobs.start(this._uri, {
                action: "import",
                dryrun: dryRun ? "true" : "false",
                overwrite: this._overwrite.checked ? "true" : "false"
            }, this._file);

            const report = await this._follow(job);

            // an import changed what a check said; the next import checks again
            this._checked = dryRun ? report : null;

            return report;
        } catch (error) {
            kleenestar.core.WorkspaceTransferJobs.fail(this._panel, error?.message || this._words.errorRequest);

            return null;
        } finally {
            this._busy = false;
            this._enable();
        }
    }

    /**
     * Follows a job and shows its report.
     * @param {object} job - The job.
     * @returns {Promise<object|null>} The report.
     */
    async _follow(job) {
        const ended = await kleenestar.core.WorkspaceTransferJobs.follow(this._uri, job, (x) => kleenestar.core.WorkspaceTransferJobs.draw(this._panel, x));

        if (ended.report) {
            this._show(ended.report);
        }

        return ended.report;
    }

    /**
     * Picks up an import that still runs after a reload.
     */
    async _resume() {
        const job = await kleenestar.core.WorkspaceTransferJobs.latest(this._uri, "import");

        if (!job || job.state === "finished" || job.state === "failed") {
            return;
        }

        this._busy = true;
        this._enable();

        try {
            await this._follow(job);
        } catch {
            this._panel.hidden = true;
        } finally {
            this._busy = false;
            this._enable();
        }
    }

    /**
     * Enables the buttons that may be used now.
     */
    _enable() {
        this._check.disabled = this._busy || !this._file;
        this._run.disabled = this._busy || !this._file;
        this._drop.classList.toggle("ks-transfer-drop-busy", this._busy);
    }

    /**
     * Shows a single message in the result area.
     * @param {string} text - The message.
     * @param {string} tone - "info", "success" or "danger".
     */
    _message(text, tone) {
        this._result.replaceChildren(this._callout(text, tone));
    }

    /**
     * Returns a callout.
     * @param {string} text - The text.
     * @param {string} tone - "info", "success", "warning" or "danger".
     * @returns {HTMLElement} The callout.
     */
    _callout(text, tone) {
        const callout = document.createElement("div");
        const icon = document.createElement("i");
        const body = document.createElement("span");
        const glyph = { success: "circle-check", danger: "triangle-exclamation", warning: "triangle-exclamation", info: "circle-info" }[tone] || "circle-info";

        callout.className = `ks-transfer-callout ks-transfer-callout-${tone}`;
        icon.className = `wx-icon-light wx-icon-light-${glyph}`;
        icon.setAttribute("aria-hidden", "true");
        body.textContent = text || "";
        callout.append(icon, body);

        return callout;
    }

    /**
     * Lists a report: the outcome, the workspaces, the tables and the notes.
     * @param {object} report - The report.
     */
    _show(report) {
        const words = this._words;
        const tone = !report.succeeded ? "danger" : report.dryRun ? "info" : "success";
        const outcome = report.error || (report.dryRun ? words.resultChecked : words.resultImported);

        this._result.replaceChildren(this._callout(outcome, tone));

        // the workspaces, each with what happens to it
        if (Array.isArray(report.workspaces) && report.workspaces.length > 0) {
            const list = document.createElement("div");

            list.className = "ks-transfer-outcome";

            for (const workspace of report.workspaces) {
                const row = document.createElement("div");
                const name = document.createElement("span");
                const key = document.createElement("span");
                const chip = document.createElement("span");
                const state = !workspace.imported ? "skipped" : workspace.replaced ? "replaced" : "new";

                row.className = `ks-transfer-outcome-row ks-transfer-outcome-${state}`;
                name.className = "ks-transfer-outcome-name";
                name.textContent = workspace.name || "";
                key.className = "ks-transfer-chip";
                key.textContent = workspace.key || "";
                chip.className = `ks-transfer-state ks-transfer-state-${state}`;
                chip.textContent = { new: words.resultNew, replaced: words.resultReplaced, skipped: words.resultSkipped }[state] || "";

                row.append(name, key, chip);

                if (!workspace.imported && workspace.reason) {
                    const reason = document.createElement("span");

                    reason.className = "ks-transfer-outcome-reason";
                    reason.textContent = workspace.reason;
                    row.append(reason);
                }

                list.append(row);
            }

            this._result.append(list);
        }

        // the tables and the notes, each in a section of the framework (wx-webui-section): the
        // controller adopts the inserted element and builds the foldable header around it
        const tables = Array.isArray(report.tables) ? report.tables : [];
        const notes = Array.isArray(report.notes) ? report.notes : [];

        if (tables.length > 0) {
            const columns = [
                ["rows", words.resultRows],
                ["written", words.resultWritten],
                ["updated", words.resultUpdated],
                ["deleted", words.resultDeleted],
                ["reused", words.resultReused],
                ["skipped", words.resultLeft]
            ].filter(([key]) => key === "rows" || tables.some((x) => Number(x[key]) > 0));

            const table = document.createElement("table");
            const thead = document.createElement("thead");
            const head = document.createElement("tr");
            const body = document.createElement("tbody");

            table.className = "table table-sm ks-transfer-table";

            for (const title of [words.resultTable, ...columns.map(([, title]) => title)]) {
                const cell = document.createElement("th");

                cell.textContent = title || "";
                head.append(cell);
            }

            for (const row of tables) {
                const line = document.createElement("tr");
                const name = document.createElement("td");

                name.textContent = row.name;
                line.append(name);

                for (const [key] of columns) {
                    const cell = document.createElement("td");
                    const value = Number(row[key]) || 0;

                    cell.textContent = value === 0 && key !== "rows" ? "\u2013" : String(value);
                    cell.className = value > 0 ? `ks-transfer-count-${key}` : "ks-transfer-count-zero";
                    line.append(cell);
                }

                body.append(line);
            }

            thead.append(head);
            table.append(thead, body);

            this._result.append(this._section(words.resultDetails, "wx-icon-light wx-icon-light-table", tables.length, false, table));
        }

        if (notes.length > 0) {
            const list = document.createElement("ul");

            list.className = "ks-transfer-notes";

            for (const note of notes) {
                const item = document.createElement("li");

                item.textContent = note;
                list.append(item);
            }

            // a report that left something out shows why
            const open = !report.succeeded || !report.dryRun;

            this._result.append(this._section(words.resultNotes, "wx-icon-light wx-icon-light-circle-info", notes.length, open, list));
        }
    }

    /**
     * Returns a section of the framework (webexpress.webui.SectionCtrl) around content: the
     * header with icon and badge, folded or open, in the rule layout of the settings pages.
     * @param {string} header - The header.
     * @param {string} icon - The icon classes.
     * @param {number} badge - The number shown beside the header.
     * @param {boolean} expanded - Whether the body is shown.
     * @param {HTMLElement} content - The body.
     * @returns {HTMLElement} The section host.
     */
    _section(header, icon, badge, expanded, content) {
        const section = document.createElement("div");

        section.className = "wx-webui-section wx-section-rule ks-transfer-section";
        section.dataset.header = header || "";
        section.dataset.headerIconCss = icon;
        section.dataset.badge = String(badge);
        section.dataset.expanded = expanded ? "true" : "false";
        section.dataset.guide = "false";
        section.dataset.persist = "false";
        section.append(content);

        return section;
    }
};

// register the class in the controller
webexpress.webui.Controller.registerClass("wx-kleenestar-transferimport", kleenestar.core.WorkspaceTransferImportCtrl);
