/**
 * KleeneStar: draws the reports tab of an insight.
 *
 * The server renders each reports tab as a host (.ks-insight-reports) carrying the address of
 * the insight's reports endpoint (data-uri), a translated toolbar - report, range, interval,
 * sprint count - and an empty stage. This script loads the picked report and draws it with the
 * Chart.js the framework ships on every page, then lists the key figures beneath it.
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
     * Builds the Chart.js configuration of a report answer.
     * @param {object} data The report.
     * @param {HTMLElement} host The host, for the theme colours.
     * @returns {object} The configuration.
     */
    const configure = (data, host) => {
        const style = getComputedStyle(host);
        const text = style.color || "#666";
        const grid = translucent(/^#/.test(text) ? text : "#888888", 0.15) || "rgba(128,128,128,0.15)";
        const series = Array.isArray(data.series) ? data.series : [];
        const hasY1 = series.some((s) => s.axis === "y1");
        let filled = 0;

        const datasets = series.map((s, index) => {
            const color = s.color || PALETTE[index % PALETTE.length];
            const bar = s.type === "bar";
            let fill = false;

            if (s.fill) {
                // a stacked area fills down to the band below it, the first one to the axis
                fill = data.stacked ? (filled === 0 ? "origin" : "-1") : "origin";
                filled++;
            }

            return {
                type: bar ? "bar" : "line",
                label: s.label,
                data: s.data,
                yAxisID: s.axis === "y1" ? "y1" : "y",
                borderColor: color,
                backgroundColor: bar ? translucent(color, 0.75) : (s.fill ? translucent(color, 0.45) : color),
                borderWidth: bar ? 0 : 2,
                fill,
                // monotone: a smoothed line never dips below or rises above its points, which a
                // count over time must not appear to do
                cubicInterpolationMode: "monotone",
                pointRadius: bar ? 0 : 2,
                spanGaps: true,
                // lines are drawn over the bars
                order: bar ? 2 : 1
            };
        });

        const scales = {
            x: {
                stacked: !!data.stacked,
                ticks: { color: text, maxRotation: 0, autoSkip: true },
                grid: { color: grid }
            },
            y: {
                stacked: !!data.stacked,
                beginAtZero: true,
                ticks: { color: text, precision: 0 },
                grid: { color: grid },
                title: { display: !!data.axisY, text: data.axisY || "", color: text }
            }
        };

        if (hasY1) {
            scales.y1 = {
                position: "right",
                beginAtZero: true,
                ticks: { color: text, precision: 0 },
                grid: { drawOnChartArea: false },
                title: { display: !!data.axisY1, text: data.axisY1 || "", color: text }
            };
        }

        return {
            type: data.chart === "bar" ? "bar" : "line",
            data: { labels: data.labels || [], datasets },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: false,
                interaction: { mode: "index", intersect: false },
                plugins: {
                    legend: { position: "bottom", labels: { color: text } },
                    tooltip: { mode: "index", intersect: false }
                },
                scales
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
        const canvas = host.querySelector(".ks-insight-reports-stage canvas");
        const empty = host.querySelector(".ks-insight-reports-empty");
        const error = host.querySelector(".ks-insight-reports-error");
        const notice = host.querySelector(".ks-insight-reports-notice");
        const summary = host.querySelector(".ks-insight-reports-summary");
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

                if (chart) {
                    chart.destroy();
                    chart = null;
                }

                canvas.hidden = !!data.empty;

                if (!data.empty && typeof window.Chart === "function") {
                    chart = new window.Chart(canvas, configure(data, host));
                }
            } catch (e) {
                if (current !== sequence) {
                    return;
                }

                console.warn("[KleeneStar] report could not be loaded:", e);
                error.hidden = false;
                empty.hidden = true;
                canvas.hidden = true;
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
