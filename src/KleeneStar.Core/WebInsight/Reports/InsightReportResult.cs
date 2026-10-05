using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KleeneStar.Core.WebInsight.Reports
{
    /// <summary>
    /// A computed report as the reports tab draws it: the x labels, the series and a few key
    /// figures beneath the chart.
    /// </summary>
    public sealed class InsightReportResult
    {
        /// <summary>
        /// Gets or sets the key of the report.
        /// </summary>
        [JsonPropertyName("report")]
        public string Report { get; set; }

        /// <summary>
        /// Gets or sets the title of the report.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the sentence that says what the report shows and how it is computed.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the chart the report is drawn as: <c>line</c> or <c>bar</c>.
        /// </summary>
        [JsonPropertyName("chart")]
        public string Chart { get; set; } = "line";

        /// <summary>
        /// Gets or sets a value indicating whether the series of the left axis are stacked.
        /// </summary>
        [JsonPropertyName("stacked")]
        public bool Stacked { get; set; }

        /// <summary>
        /// Gets or sets the title of the left axis.
        /// </summary>
        [JsonPropertyName("axisY")]
        public string AxisY { get; set; }

        /// <summary>
        /// Gets or sets the title of the right axis, or null when no series uses it.
        /// </summary>
        [JsonPropertyName("axisY1")]
        public string AxisY1 { get; set; }

        /// <summary>
        /// Gets or sets the x labels.
        /// </summary>
        [JsonPropertyName("labels")]
        public IReadOnlyList<string> Labels { get; set; } = [];

        /// <summary>
        /// Gets or sets the series.
        /// </summary>
        [JsonPropertyName("series")]
        public IReadOnlyList<InsightReportSeries> Series { get; set; } = [];

        /// <summary>
        /// Gets or sets the key figures shown beneath the chart.
        /// </summary>
        [JsonPropertyName("summary")]
        public IReadOnlyList<InsightReportFigure> Summary { get; set; } = [];

        /// <summary>
        /// Gets or sets a value indicating whether the report has nothing to show.
        /// </summary>
        [JsonPropertyName("empty")]
        public bool Empty { get; set; }

        /// <summary>
        /// Gets or sets a remark on what the report could not take into account, or null.
        /// </summary>
        [JsonPropertyName("notice")]
        public string Notice { get; set; }
    }

    /// <summary>
    /// One series of a report.
    /// </summary>
    public sealed class InsightReportSeries
    {
        /// <summary>
        /// Gets or sets the key of the series.
        /// </summary>
        [JsonPropertyName("key")]
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the label of the series.
        /// </summary>
        [JsonPropertyName("label")]
        public string Label { get; set; }

        /// <summary>
        /// Gets or sets how the series is drawn: <c>line</c> or <c>bar</c>.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "line";

        /// <summary>
        /// Gets or sets the colour of the series, or null for the chart's palette.
        /// </summary>
        [JsonPropertyName("color")]
        public string Color { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the area under a line is filled.
        /// </summary>
        [JsonPropertyName("fill")]
        public bool Fill { get; set; }

        /// <summary>
        /// Gets or sets the axis the series is read on: <c>y</c> or <c>y1</c>.
        /// </summary>
        [JsonPropertyName("axis")]
        public string Axis { get; set; } = "y";

        /// <summary>
        /// Gets or sets the values, one per label; null where there is no value.
        /// </summary>
        [JsonPropertyName("data")]
        public IReadOnlyList<double?> Data { get; set; } = [];
    }

    /// <summary>
    /// A key figure of a report.
    /// </summary>
    /// <param name="Label">What the figure is.</param>
    /// <param name="Value">The figure, formatted.</param>
    public sealed record InsightReportFigure
    (
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("value")] string Value
    );

    /// <summary>
    /// A status category as a report draws it.
    /// </summary>
    /// <param name="Key">The normalized key, for example <c>inprogress</c>.</param>
    /// <param name="Label">The label.</param>
    /// <param name="Color">The colour, or null.</param>
    public sealed record InsightReportCategory(string Key, string Label, string Color);

    /// <summary>
    /// A sprint as the velocity report reads it.
    /// </summary>
    /// <param name="Id">The sprint id.</param>
    /// <param name="Label">The label, carrying the workspace when the insight spans several.</param>
    /// <param name="Start">The start, if planned.</param>
    /// <param name="End">The end, if planned.</param>
    /// <param name="Completed">Whether the sprint was completed.</param>
    /// <param name="Active">Whether the sprint is running.</param>
    public sealed record InsightReportSprint(Guid Id, string Label, DateTime? Start, DateTime? End, bool Completed, bool Active);
}
