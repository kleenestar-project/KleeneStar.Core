var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * The background jobs of the workspace transfer settings page, shared by its export and import
 * tabs: starts a job at the transfer endpoint, follows it until it ends and draws its progress
 * panel (.ks-transfer-job: phase, share, bar).
 *
 * The endpoint answers a job as { id, kind, state, progress, phase, download, fileName, size,
 * report }; a job is followed by asking for it again every few hundred milliseconds until its
 * state is "finished" or "failed".
 */
kleenestar.core.WorkspaceTransferJobs = new class {
    /**
     * The pause between two questions, in milliseconds.
     */
    static INTERVAL = 700;

    /**
     * Starts a job.
     * @param {string} uri - The address of the transfer endpoint.
     * @param {object} parameters - The query parameters.
     * @param {Blob} [body] - The archive of an import.
     * @returns {Promise<object>} The job.
     */
    async start(uri, parameters, body) {
        const response = await fetch(this.address(uri, parameters), {
            method: "POST",
            credentials: "same-origin",
            headers: body
                ? { "Content-Type": "application/zip", "Accept": "application/json" }
                : { "Accept": "application/json" },
            body: body || null
        });

        const payload = await this._json(response);

        if (!response.ok || !payload || !payload.id) {
            throw new Error(payload?.message || `status ${response.status}`);
        }

        return payload;
    }

    /**
     * Follows a job until it ends.
     * @param {string} uri - The address of the transfer endpoint.
     * @param {object} job - The job as the endpoint answered it.
     * @param {function(object): void} update - Called with every state of the job.
     * @returns {Promise<object>} The job as it ended.
     */
    async follow(uri, job, update) {
        let current = job;

        update(current);

        while (current.state !== "finished" && current.state !== "failed") {
            await new Promise((resolve) => setTimeout(resolve, this.constructor.INTERVAL));

            const response = await fetch(this.address(uri, { job: current.id }), {
                credentials: "same-origin",
                headers: { "Accept": "application/json" }
            });
            const payload = await this._json(response);

            if (!response.ok || !payload) {
                throw new Error(`status ${response.status}`);
            }

            current = payload;
            update(current);
        }

        return current;
    }

    /**
     * Returns the latest job of a kind, or null.
     * @param {string} uri - The address of the transfer endpoint.
     * @param {string} kind - "export" or "import".
     * @returns {Promise<object|null>} The job.
     */
    async latest(uri, kind) {
        try {
            const response = await fetch(uri, { credentials: "same-origin", headers: { "Accept": "application/json" } });
            const payload = await this._json(response);

            return (payload?.jobs || []).find((x) => x.kind === kind) || null;
        } catch {
            return null;
        }
    }

    /**
     * Draws the progress panel of a job.
     * @param {HTMLElement} panel - The panel (.ks-transfer-job).
     * @param {object} job - The job.
     */
    draw(panel, job) {
        if (!panel) {
            return;
        }

        const bar = panel.querySelector(".ks-transfer-progress-bar");
        const track = panel.querySelector(".ks-transfer-progress");
        const percent = Math.max(0, Math.min(100, Number(job.progress) || 0));
        const ended = job.state === "finished" || job.state === "failed";
        const failed = job.state === "failed" || (ended && job.report && !job.report.succeeded);

        panel.hidden = false;
        panel.classList.toggle("ks-transfer-job-ended", ended);
        panel.classList.toggle("ks-transfer-job-failed", failed);
        panel.querySelector(".ks-transfer-job-phase").textContent = job.phase || "";
        panel.querySelector(".ks-transfer-job-percent").textContent = `${Math.round(percent)} %`;

        bar.style.width = `${percent}%`;
        bar.classList.toggle("progress-bar-animated", !ended);
        bar.classList.toggle("progress-bar-striped", !ended);
        bar.classList.toggle("bg-success", ended && !failed);
        bar.classList.toggle("bg-danger", failed);
        track.setAttribute("aria-valuenow", String(Math.round(percent)));
    }

    /**
     * Shows an error in the progress panel.
     * @param {HTMLElement} panel - The panel (.ks-transfer-job).
     * @param {string} text - The error.
     */
    fail(panel, text) {
        const error = panel?.querySelector(".ks-transfer-job-error");

        if (!error) {
            return;
        }

        panel.hidden = false;
        error.hidden = !text;
        error.textContent = text || "";
    }

    /**
     * Returns an address with query parameters.
     * @param {string} uri - The address.
     * @param {object} parameters - The parameters.
     * @returns {string} The address.
     */
    address(uri, parameters) {
        const query = Object.entries(parameters || {})
            .filter(([, value]) => value !== undefined && value !== null && value !== "")
            .map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(value)}`)
            .join("&");

        return query ? `${uri}${uri.includes("?") ? "&" : "?"}${query}` : uri;
    }

    /**
     * Returns a size in words: bytes, kilobytes or megabytes.
     * @param {number} bytes - The size.
     * @returns {string} The size.
     */
    size(bytes) {
        const value = Number(bytes) || 0;

        if (value >= 1024 * 1024) {
            return `${(value / (1024 * 1024)).toFixed(1)} MB`;
        }

        if (value >= 1024) {
            return `${Math.round(value / 1024)} KB`;
        }

        return `${value} B`;
    }

    /**
     * Reads a JSON answer, or null.
     * @param {Response} response - The answer.
     * @returns {Promise<object|null>} The payload.
     */
    async _json(response) {
        try {
            return await response.json();
        } catch {
            return null;
        }
    }
};
