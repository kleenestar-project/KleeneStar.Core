/**
 * KleeneStar: draws the reports tab of an insight.
 *
 * The server renders each reports tab as a host (.ks-insight-reports) carrying the address of
 * the insight's reports endpoint (data-uri), a translated toolbar - report, range, interval,
 * sprint count - and an empty stage. This script loads the picked report and draws it with the
 * framework's chart control (webexpress.webui.ChartCtrl, SVG, theme-aware), then lists the key
 * figures beneath it.
 *
 * A series the report puts on the second axis ("y1": the open or resolved count beside an age
 * or a flow) is measured on the control's second value axis at the right edge, titled by the
 * report's axisY1, so a count and a duration do not share one scale.
 *
 * The tab control renders a tab template once and clones it into a pane per tab, and adds panes
 * when a tab is added, so the script does not initialize at load alone: it watches the document
 * and takes over every host that becomes visible and is not yet its own. The reader's choice is
 * remembered per insight in the browser (data-storage), never on the server.
 */
(function () {
    const HOST = ".ks-insight-reports";
    const PALETTE = ["#33C1FF", "#FF5733", "#28A745", "#FFC300", "#6F42C1", "#20C997", "#E83E8C", "#9AA5B1"];
    const owned = new WeakSet();

    /**
     * Reads the remembered choice of a host.
     * @param {HTMLElement} host The host.
     * @returns {object} The choice, or an empty object.
     */
    const recall = (host) => {
        try {
            const raw = window.localStorage.getItem(host.dataset.storage || "");
            return raw ? JSON.parse(raw) || {} : {};
        } catch {
            return {};
        }
    };

    /**
     * Remembers the choice of a host.
     * @param {HTMLElement} host The host.
     * @param {object} choice The choice.
     */
    const remember = (host, choice) => {
        try {
            if (host.dataset.storage) {
                window.localStorage.setItem(host.dataset.storage, JSON.stringify(choice));
            }
        } catch {
            // a browser that keeps nothing still draws the report
        }
    };

    /**
     * Turns a colour into a translucent one for areas and bars.
     * @param {string} color A #rrggbb colour.
     * @param {number} alpha The opacity.
     * @returns {string} The colour.
     */
    const translucent = (color, alpha) => {
        const match = /^#?([0-9a-f]{6})$/i.exec(color || "");

        if (!match) {
            return color;
        }

        const value = parseInt(match[1], 16);

        return `rgba(${(value >> 16) & 255}, ${(value >> 8) & 255}, ${value & 255}, ${alpha})`;
    };

    /**
     * Sets a select to a value when it offers it.
     * @param {HTMLSelectElement} select The select.
     * @param {any} value The value.
     */
    const choose = (select, value) => {
        if (select && value != null && Array.from(select.options).some((o) => o.value === String(value))) {
            select.value = String(value);
        }
    };

    /**
     * Builds the chart control's configuration of a report answer.
     * @param {object} data The report.
     * @returns {object} The configuration { type, data, options }.
     */
    const configure = (data) => {
        const series = Array.isArray(data.series) ? data.series : [];
        const second = series.some((s) => s.axis === "y1");

        const datasets = series.map((s, index) => {
            const color = s.color || PALETTE[index % PALETTE.length];
            const bar = s.type === "bar";

            return {
                type: bar ? "bar" : "line",
                label: s.label,
                data: s.data,
                borderColor: color,
                // a stacked band is painted flat; the control's own fade suits a single area,
                // not bands resting on each other
                backgroundColor: bar ? color : (s.fill ? translucent(color, 0.45) : color),
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
    };

    /**
     * Takes over a host: wires its toolbar and draws the first report.
     * @param {HTMLElement} host The host.
     */
    const adopt = (host) => {
        owned.add(host);

        const report = host.querySelector(".ks-insight-reports-report");
        const range = host.querySelector(".ks-insight-reports-range");
        const interval = host.querySelector(".ks-insight-reports-interval");
        const sprints = host.querySelector(".ks-insight-reports-sprintcount");
        const description = host.querySelector(".ks-insight-reports-description");
        const plot = host.querySelector(".ks-insight-reports-chart");
        const empty = host.querySelector(".ks-insight-reports-empty");
        const error = host.querySelector(".ks-insight-reports-error");
        const notice = host.querySelector(".ks-insight-reports-notice");
        const summary = host.querySelector(".ks-insight-reports-summary");
        const csv = host.querySelector(".ks-insight-reports-csv");
        let chart = null;
        let sequence = 0;

        const choice = recall(host);
        choose(report, choice.report);
        choose(range, choice.range);
        choose(interval, choice.interval);
        choose(sprints, choice.sprints);

        /**
         * Shows the fields the picked report reads: the velocity chart is cut by sprints, the
         * others by time.
         */
        const arrange = () => {
            const velocity = report.value === "velocity";

            host.querySelectorAll(".ks-insight-reports-period").forEach((x) => { x.hidden = velocity; });
            host.querySelectorAll(".ks-insight-reports-sprints").forEach((x) => { x.hidden = !velocity; });
        };

        /**
         * Loads the picked report and draws it.
         */
        const load = async () => {
            const current = ++sequence;
            const params = new URLSearchParams({
                report: report.value,
                range: range.value,
                interval: interval.value,
                sprints: sprints.value
            });

            arrange();
            remember(host, { report: report.value, range: range.value, interval: interval.value, sprints: sprints.value });

            if (csv) {
                const base = host.dataset.uri || "";
                csv.href = `${base}${base.includes("?") ? "&" : "?"}${params}&format=csv`;
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
                if (current !== sequence) {
                    return;
                }

                description.textContent = data.description || "";
                error.hidden = true;
                empty.hidden = !data.empty;
                notice.hidden = !data.notice;
                notice.textContent = data.notice || "";

                summary.replaceChildren(...(data.summary || []).map((figure) => {
                    const item = document.createElement("div");
                    const label = document.createElement("dt");
                    const value = document.createElement("dd");

                    label.textContent = figure.label;
                    value.textContent = figure.value;
                    item.append(label, value);

                    return item;
                }));

                plot.hidden = !!data.empty;

                if (!data.empty) {
                    // one controller per host; a later pick replaces its configuration and the
                    // chart eases into the new report
                    if (chart) {
                        chart.config(configure(data));
                    } else if (window.webexpress?.webui?.ChartCtrl) {
                        chart = new window.webexpress.webui.ChartCtrl(plot, configure(data));
                    }
                }
            } catch (e) {
                if (current !== sequence) {
                    return;
                }

                console.warn("[KleeneStar] report could not be loaded:", e);
                error.hidden = false;
                empty.hidden = true;
                plot.hidden = true;
                summary.replaceChildren();
            } finally {
                if (current === sequence) {
                    host.classList.remove("ks-insight-reports-loading");
                }
            }
        };

        [report, range, interval, sprints].forEach((select) => select?.addEventListener("change", load));

        load();
    };

    /**
     * Takes over every visible host that is not yet taken over. A host in a pane that is not
     * shown waits: a chart drawn into a box without a size has none.
     */
    const scan = () => {
        document.querySelectorAll(HOST).forEach((host) => {
            if (!owned.has(host) && host.getClientRects().length > 0 && host.dataset.uri) {
                adopt(host);
            }
        });
    };

    let pending = false;

    /**
     * Schedules a scan for the next frame, so a burst of mutations scans once.
     */
    const schedule = () => {
        if (!pending) {
            pending = true;
            window.requestAnimationFrame(() => {
                pending = false;
                scan();
            });
        }
    };

    const start = () => {
        scan();

        new MutationObserver(schedule).observe(document.body, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ["class", "hidden", "style"]
        });
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", start);
    } else {
        start();
    }
})();
