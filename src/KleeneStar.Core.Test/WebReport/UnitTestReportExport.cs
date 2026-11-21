using KleeneStar.Core.WebInsight.Reports;
using KleeneStar.Core.WebReport;
using KleeneStar.Model.Entities;
using System.Globalization;
using WebExpress.WebUI.WebControl;
using Export = KleeneStar.Core.WWW.Api._1_.Insights._insightid_.Export;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebReport
{
    /// <summary>
    /// Tests the reports and exports: CSV that a spreadsheet reads as written and does not run,
    /// the service level compliance report, and the object export with its columns.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestReportExport
    {
        /// <summary>
        /// Cells with separators, quotes and line breaks are quoted; a cell a spreadsheet would
        /// run as a formula is defused; the semicolon separator is honoured.
        /// </summary>
        [Fact]
        public void Csv_EscapesAndDefuses()
        {
            var comma = new CsvDocument().Row("a,b", "say \"hi\"", "line\nbreak", "=HYPERLINK(\"x\")", "-1", "plain").ToString();

            Assert.Equal("\"a,b\",\"say \"\"hi\"\"\",\"line\nbreak\",\"'=HYPERLINK(\"\"x\"\")\",'-1,plain\r\n", comma);

            var semicolon = new CsvDocument(';').Row("a,b", "c;d").ToString();

            Assert.Equal("a,b;\"c;d\"\r\n", semicolon);
        }

        /// <summary>
        /// The download starts with a byte order mark and is named as a CSV file.
        /// </summary>
        [Fact]
        public void Csv_DownloadCarriesBomAndName()
        {
            var response = new CsvDocument().Row("ä").ToResponse("My insight: Q3.csv");

            Assert.Equal([0xEF, 0xBB, 0xBF], response.Content is byte[] bytes ? bytes[..3] : null);
            Assert.StartsWith("text/csv", response.Header.ContentType, StringComparison.Ordinal);
            Assert.Contains("filename=\"My-insight--Q3.csv\"", response.Header.ContentDisposition, StringComparison.Ordinal);
        }

        /// <summary>
        /// The compliance report counts the targets of the objects created in the period per
        /// outcome, and the rate is met of decided.
        /// </summary>
        [Fact]
        public void SlaReport_CountsOutcomesAndRate()
        {
            var now = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
            var period = InsightReportPeriod.Create(now, 30, InsightReportInterval.Week);
            var inside = now.AddDays(-3);

            var items = new List<InsightSlaItem>
            {
                new("SD-1", inside, "Standard", "Response", TypeStatusSla.Fulfilled, true, null),
                new("SD-1", inside, "Standard", "Resolution", TypeStatusSla.Violated, true, null),
                new("SD-2", inside, "Standard", "Resolution", TypeStatusSla.Violated, false, null),
                new("SD-3", inside, "Standard", "Response", TypeStatusSla.AtRisk, false, null),
                new("SD-4", inside, "Standard", "Response", TypeStatusSla.Fulfilled, false, null),
                new("SD-5", now.AddDays(-90), "Standard", "Response", TypeStatusSla.Violated, true, null)
            };

            var result = InsightSlaReport.Build(items, period, key => key, CultureInfo.InvariantCulture);

            double Total(string key) => result.Series.Single(x => x.Key == key).Data.Sum(x => x ?? 0);

            Assert.Equal(1, Total(InsightSlaReport.Met));
            Assert.Equal(2, Total(InsightSlaReport.Breached));
            Assert.Equal(1, Total(InsightSlaReport.AtRisk));
            Assert.Equal(1, Total(InsightSlaReport.Running));
            Assert.Equal("33.3 %", result.Summary[0].Value);
            Assert.Contains(result.Summary, x => x.Value == "Standard · Resolution (2)");
            Assert.False(result.Empty);
        }

        /// <summary>
        /// The export reads the columns asked for, in their order, and falls back to the
        /// defaults for none.
        /// </summary>
        [Fact]
        public void Export_ParsesColumns()
        {
            Assert.Equal(Export.DefaultColumns, Export.ParseColumns(null));
            Assert.Equal(Export.DefaultColumns, Export.ParseColumns("bogus, , "));
            Assert.Equal(["summary", "key", "field:Impact"], Export.ParseColumns("Summary,key,unknown,field: Impact,key"));
        }

        /// <summary>
        /// The export writes one row per object with the names resolved and a field's value.
        /// </summary>
        [Fact]
        public void Export_WritesObjects()
        {
            var database = nameof(Export_WritesObjects);
            CoreHubFixture.Initialize(database);

            var workspaceId = Guid.NewGuid();
            var classId = Guid.NewGuid();
            var fieldId = Guid.NewGuid();
            var objectId = Guid.NewGuid();
            var assigneeId = Guid.NewGuid();

            using (var db = CoreHubFixture.CreateDbContext(database))
            {
                db.Workspaces.Add(new Workspace { Id = workspaceId, Key = "OPS", Name = "Operations" });
                db.Classes.Add(new Class { Id = classId, Name = "Incident", WorkspaceId = workspaceId });
                db.Fields.Add(new Field { Id = fieldId, Name = "Impact", ClassId = classId, FieldType = FieldType.Text, State = FieldState.Active });
                db.Identities.Add(new Identity { Id = assigneeId, Name = "Agent Smith", UserName = "agent", Email = "agent@example.com", PasswordHash = "x" });
                db.Objects.Add(new ObjectEntity(objectId) { Key = "OPS-1", Summary = "=cmd|' /C calc'!A0", WorkspaceId = workspaceId, ClassId = classId, AssigneeId = assigneeId, Created = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc) });
                db.Values.Add(new Value { ObjectId = objectId, FieldId = fieldId, Data = "High" });
                db.SaveChanges();
            }

            var csv = Export.Write([CoreHub.ObjectManager.GetObject(objectId)], ["key", "summary", "workspace", "class", "assignee", "created", "field:Impact"], ',', DateTime.UtcNow).ToString();
            var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal("key,summary,workspace,class,assignee,created,field:Impact", lines[0]);
            Assert.Equal("OPS-1,'=cmd|' /C calc'!A0,OPS,Incident,Agent Smith,2026-10-01T08:00:00Z,High", lines[1]);
        }
    }
}
