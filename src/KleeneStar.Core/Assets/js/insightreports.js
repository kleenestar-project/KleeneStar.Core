var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * The reports tab of an insight: `wx-kleenestar-insightreports` on the host InsightReportControl
 * renders (.ks-insight-reports), carrying the address of the insight's reports endpoint
 * (data-uri), a translated toolbar - report, range, interval, sprint count - and an empty stage.
 * The control loads the picked report and draws it with the framework's chart control
 * (webexpress.webui.ChartCtrl, SVG, theme-aware), then lists the key figures beneath it.
 *
 * A series the report puts on the second axis ("y1": the open or resolved count beside an age
 * or a flow) is measured on the control's second value axis at the right edge, titled by the
 * report's axisY1, so a count and a duration do not share one scale.
 *
 * The tab control keeps a tab template as a <template> and fills a pane per tab from it, so the
 * controller creates a control per pane as the pane is inserted. A pane that is not shown has no
 * size, and a chart drawn into it would have none either; the control therefore starts once its
 * host gets a size. The reader's choice is remembered per insight in the browser
 * (data-storage), never on the server.
 */
kleenestar.core.InsightReportsCtrl = class extends webexpress.webui.Ctrl {
    /**
     * The colours of the series that name none.
     */
    static PALETTE = ["#33C1FF", "#FF5733", "#28A745", "#FFC300", "#6F42C1", "#20C997", "#E83E8C", "#9AA5B1"];

    /**
     * Constructor
     * @param {HTMLElement} element - The DOM element associated with the instance.
     */
    constructor(element) {
        super(element);

        this._chart = null;
        this._sequence = 0;
        this._started = false;

        // a host in a pane that is not shown waits until it is
        this._observer = new ResizeObserver(() => {
            if (!this._started && element.getClientRects().length > 0 && element.dataset.uri) {
                this._observer.disconnect();
                this._start();
            }
        });
        this._observer.observe(element);
    }

    /**
     * Wires the toolbar and draws the first report.
     */
    _start() {
        const host = this._element;

        this._started = true;
        this._report = host.querySelector(".ks-insight-reports-report");
        this._range = host.querySelector(".ks-insight-reports-range");
        this._interval = host.querySelector(".ks-insight-reports-interval");
        this._sprints = host.querySelector(".ks-insight-reports-sprintcount");
        this._description = host.querySelector(".ks-insight-reports-description");
        this._plot = host.querySelector(".ks-insight-reports-chart");
        this._empty = host.querySelector(".ks-insight-reports-empty");
        this._error = host.querySelector(".ks-insight-reports-error");
        this._notice = host.querySelector(".ks-insight-reports-notice");
        this._summary = host.querySelector(".ks-insight-reports-summary");
        this._csv = host.querySelector(".ks-insight-reports-csv");

        const choice = this._recall();
        this._choose(this._report, choice.report);
        this._choose(this._range, choice.range);
        this._choose(this._interval, choice.interval);
        this._choose(this._sprints, choice.sprints);

        [this._report, this._range, this._interval, this._sprints].forEach((select) => select?.addEventListener("change", () => this.load()));

        this.load();
    }

    /**
     * Loads the picked report and draws it.
     */
    async load() {
        const host = this._element;
        const current = ++this._sequence;
        const params = new URLSearchParams({
            report: this._report.value,
            range: this._range.value,
            interval: this._interval.value,
            sprints: this._sprints.value
        });

        this._arrange();
        this._remember({ report: this._report.value, range: this._range.value, interval: this._interval.value, sprints: this._sprints.value });

        if (this._csv) {
            const base = host.dataset.uri || "";
            this._csv.href = `${base}${base.includes("?") ? "&" : "?"}${params}&format=csv`;
        }
        host.classList.add("ks-insight-reports-loading");

        try {
            const uri = host.dataset.uri || "";
            const response = await fetch(`${uri}${uri.includes("?") ? "&" : "?"}${params}`, {
                credentials: "same-origin",
                headers: { "Accept": "application/json" }
            });

            if (!response.ok) {
                throw new Error(`status ${response.status}`);
            }

            const data = await response.json();

            // a slower answer to an earlier pick must not overwrite a later one
            if (current !== this._sequence) {
                return;
            }

            this._description.textContent = data.description || "";
            this._error.hidden = true;
            this._empty.hidden = !data.empty;
            this._notice.hidden = !data.notice;
            this._notice.textContent = data.notice || "";

            this._summary.replaceChildren(...(data.summary || []).map((figure) => {
                const item = document.createElement("div");
                const label = document.createElement("dt");
                const value = document.createElement("dd");

                label.textContent = figure.label;
                value.textContent = figure.value;
                item.append(label, value);

                return item;
            }));

            this._plot.hidden = !!data.empty;

            if (!data.empty) {
                // one chart per host; a later pick replaces its configuration and the chart
                // eases into the new report
                if (this._chart) {
                    this._chart.config(this._configure(data));
                } else {
                    this._chart = new webexpress.webui.ChartCtrl(this._plot, this._configure(data));
                }
            }
        } catch (e) {
            if (current !== this._sequence) {
                return;
            }

            console.warn("[KleeneStar] report could not be loaded:", e);
            this._error.hidden = false;
            this._empty.hidden = true;
            this._plot.hidden = true;
            this._summary.replaceChildren();
        } finally {
            if (current === this._sequence) {
                host.classList.remove("ks-insight-reports-loading");
            }
        }
    }

    /**
     * Shows the fields the picked report reads: the velocity chart is cut by sprints, the
     * others by time.
     */
    _arrange() {
        const velocity = this._report.value === "velocity";

        this._element.querySelectorAll(".ks-insight-reports-period").forEach((x) => { x.hidden = velocity; });
        this._element.querySelectorAll(".ks-insight-reports-sprints").forEach((x) => { x.hidden = !velocity; });
    }

    /**
     * Reads the remembered choice.
     * @returns {object} The choice, or an empty object.
     */
    _recall() {
        try {
            const raw = window.localStorage.getItem(this._element.dataset.storage || "");
            return raw ? JSON.parse(raw) || {} : {};
        } catch {
            return {};
        }
    }

    /**
     * Remembers the choice.
     * @param {object} choice - The choice.
     */
    _remember(choice) {
        try {
            if (this._element.dataset.storage) {
                window.localStorage.setItem(this._element.dataset.storage, JSON.stringify(choice));
            }
        } catch {
            // a browser that keeps nothing still draws the report
        }
    }

    /**
     * Sets a select to a value when it offers it.
     * @param {HTMLSelectElement} select - The select.
     * @param {any} value - The value.
     */
    _choose(select, value) {
        if (select && value != null && Array.from(select.options).some((o) => o.value === String(value))) {
            select.value = String(value);
        }
    }

    /**
     * Turns a colour into a translucent one for areas and bars.
     * @param {string} color - A #rrggbb colour.
     * @param {number} alpha - The opacity.
     * @returns {string} The colour.
     */
    _translucent(color, alpha) {
        const match = /^#?([0-9a-f]{6})$/i.exec(color || "");

        if (!match) {
            return color;
        }

        const value = parseInt(match[1], 16);

        return `rgba(${(value >> 16) & 255}, ${(value >> 8) & 255}, ${value & 255}, ${alpha})`;
    }

    /**
     * Builds the chart control's configuration of a report answer.
     * @param {object} data - The report.
     * @returns {object} The configuration { type, data, options }.
     */
    _configure(data) {
        const palette = kleenestar.core.InsightReportsCtrl.PALETTE;
        const series = Array.isArray(data.series) ? data.series : [];
        const second = series.some((s) => s.axis === "y1");

        const datasets = series.map((s, index) => {
            const color = s.color || palette[index % palette.length];
            const bar = s.type === "bar";

            return {
                type: bar ? "bar" : "line",
                label: s.label,
                data: s.data,
                borderColor: color,
                // a stacked band is painted flat; the control's own fade suits a single area,
                // not bands resting on each other
                backgroundColor: bar ? color : (s.fill ? this._translucent(color, 0.45) : color),
                // a stacked area rests on the curve of the band beneath it, the first on the axis
                fill: s.fill ? "origin" : false,
                // monotone: a smoothed line never dips below or rises above its points, which a
                // count over time must not appear to do
                cubicInterpolationMode: "monotone",
                pointRadius: bar ? 0 : 2,
                spanGaps: true,
                yAxisID: s.axis === "y1" ? "y1" : "y"
            };
        });

        return {
            type: data.chart === "bar" ? "bar" : "line",
            data: { labels: data.labels || [], datasets },
            options: {
                responsive: true,
                plugins: {
                    legend: { position: "bottom" }
                },
                scales: {
                    x: { stacked: !!data.stacked },
                    y: {
                        stacked: !!data.stacked,
                        beginAtZero: true,
                        title: { display: !!data.axisY, text: data.axisY || "" }
                    },
                    ...(second ? {
                        y1: {
                            position: "right",
                            beginAtZero: true,
                            title: { display: !!data.axisY1, text: data.axisY1 || "" }
                        }
                    } : {})
                }
            }
        };
    }

    /**
     * Destroys the control: stops waiting for a size and drops the chart.
     */
    destroy() {
        this._observer?.disconnect();
        this._chart?.destroy?.();
    }
};

// register the class in the controller
webexpress.webui.Controller.registerClass("wx-kleenestar-insightreports", kleenestar.core.InsightReportsCtrl);
