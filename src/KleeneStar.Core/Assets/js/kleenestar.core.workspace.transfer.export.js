var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * The export tab of the workspace transfer settings page: `wx-kleenestar-transferexport` on the
 * host WorkspaceTransferExportControl renders (.ks-transfer-export), carrying the address of the
 * transfer endpoint (data-uri).
 *
 * The workspaces are cards with a checkbox; the filter hides the cards that do not match without
 * changing the choice. The export runs as a background job on the server
 * (kleenestar.core.WorkspaceTransferJobs): the button starts it, the panel beneath shows its
 * progress and, once the file is written, the link to download it. A job that still runs or
 * finished recently is picked up again after a reload.
 */
kleenestar.core.WorkspaceTransferExportCtrl = class extends webexpress.webui.Ctrl {
    /**
     * Constructor
     * @param {HTMLElement} element - The DOM element associated with the instance.
     */
    constructor(element) {
        super(element);

        this._uri = element.dataset.uri || "";
        this._cards = Array.from(element.querySelectorAll(".ks-transfer-card"));
        this._boxes = this._cards.map((x) => x.querySelector(".ks-transfer-workspace"));
        this._filter = element.querySelector(".ks-transfer-filter");
        this._count = element.querySelector(".ks-transfer-count");
        this._badge = element.querySelector(".ks-transfer-badge");
        this._start = element.querySelector(".ks-transfer-start");
        this._structureOnly = element.querySelector(".ks-transfer-structureonly");
        this._message = element.querySelector(".ks-transfer-message");
        this._empty = element.querySelector(".ks-transfer-empty");
        this._panel = element.querySelector(".ks-transfer-job");
        this._ready = element.querySelector(".ks-transfer-job-ready");
        this._download = element.querySelector(".ks-transfer-download");
        this._running = false;

        this._structureOnly.addEventListener("change", () => this._update());
        this._boxes.forEach((x) => x.addEventListener("change", () => this._update()));
        this._filter?.addEventListener("input", () => this._narrow());
        element.querySelector(".ks-transfer-all")?.addEventListener("click", () => this._choose(true));
        element.querySelector(".ks-transfer-none")?.addEventListener("click", () => this._choose(false));
        this._start?.addEventListener("click", () => this._run());

        this._update();
        this._resume();
    }

    /**
     * Checks or unchecks the visible workspaces.
     * @param {boolean} checked - Whether they are chosen.
     */
    _choose(checked) {
        this._cards.forEach((card, index) => {
            if (!card.hidden) {
                this._boxes[index].checked = checked;
            }
        });

        this._update();
    }

    /**
     * Hides the cards the filter does not match.
     */
    _narrow() {
        const text = (this._filter.value || "").trim().toLowerCase();
        let visible = 0;

        for (const card of this._cards) {
            card.hidden = text.length > 0 && !(card.dataset.search || "").includes(text);
            visible += card.hidden ? 0 : 1;
        }

        if (this._empty) {
            this._empty.hidden = visible > 0;
        }
    }

    /**
     * Marks the chosen cards and counts them.
     */
    _update() {
        const chosen = this._chosen();

        this._cards.forEach((card, index) => card.classList.toggle("ks-transfer-card-chosen", this._boxes[index].checked));

        this._count.textContent = (this._element.dataset.selected || "{0} / {1}")
            .replace("{0}", String(chosen.length))
            .replace("{1}", String(this._boxes.length));
        this._badge.textContent = String(chosen.length);
        this._message.textContent = "";
        this._start.disabled = this._running || chosen.length === 0;
        this._structureOnly.disabled = this._running;

        const included = this._element.querySelector(".ks-transfer-included");
        included.textContent = this._structureOnly.checked ? included.dataset.structure : included.dataset.full;
    }

    /**
     * Returns the ids of the chosen workspaces.
     * @returns {string[]} The ids.
     */
    _chosen() {
        return this._boxes.filter((x) => x.checked).map((x) => x.value);
    }

    /**
     * Starts the export and follows it.
     */
    async _run() {
        const chosen = this._chosen();

        if (this._running) {
            return;
        }

        if (chosen.length === 0) {
            this._message.textContent = this._element.dataset.none || "";
            return;
        }

        this._running = true;
        this._update();
        this._ready.hidden = true;
        kleenestar.core.WorkspaceTransferJobs.fail(this._panel, "");

        try {
            const all = chosen.length === this._boxes.length;
            const job = await kleenestar.core.WorkspaceTransferJobs.start(this._uri, { action: "export", ids: all ? "" : chosen.join(","), structureonly: this._structureOnly.checked });

            await this._follow(job);
        } catch (error) {
            kleenestar.core.WorkspaceTransferJobs.fail(this._panel, error?.message || this._element.dataset.failed);
        } finally {
            this._running = false;
            this._update();
        }
    }

    /**
     * Follows a job and offers its file once it is written.
     * @param {object} job - The job.
     */
    async _follow(job) {
        const ended = await kleenestar.core.WorkspaceTransferJobs.follow(this._uri, job, (x) => kleenestar.core.WorkspaceTransferJobs.draw(this._panel, x));

        if (ended.download) {
            const count = ended.report?.workspaces?.length || 0;

            this._download.href = ended.download;
            this._download.setAttribute("download", ended.fileName || "");
            this._ready.querySelector(".ks-transfer-job-ready-meta").textContent =
                `${ended.fileName || ""} · ${kleenestar.core.WorkspaceTransferJobs.size(ended.size)} · ${(this._element.dataset.workspaces || "{0}").replace("{0}", String(count))}`;
            this._ready.hidden = false;
        } else {
            kleenestar.core.WorkspaceTransferJobs.fail(this._panel, ended.report?.error || ended.report?.notes?.[0] || this._element.dataset.failed);
        }
    }

    /**
     * Picks up the latest export after a reload: one that runs is followed, a finished one
     * offers its file again.
     */
    async _resume() {
        const job = await kleenestar.core.WorkspaceTransferJobs.latest(this._uri, "export");

        if (!job || (job.state === "finished" && !job.download) || this._running) {
            return;
        }

        this._running = job.state !== "finished" && job.state !== "failed";
        this._update();

        try {
            await this._follow(job);
        } catch {
            // the job is gone: nothing to pick up
            this._panel.hidden = true;
        } finally {
            this._running = false;
            this._update();
        }
    }
};

// register the class in the controller
webexpress.webui.Controller.registerClass("wx-kleenestar-transferexport", kleenestar.core.WorkspaceTransferExportCtrl);
