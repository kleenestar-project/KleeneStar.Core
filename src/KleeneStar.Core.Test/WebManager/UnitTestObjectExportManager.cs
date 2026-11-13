using KleeneStar.Core.WebReport;
using KleeneStar.Core.WebReport.Formats;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Tests <see cref="global::KleeneStar.Core.WebManager.ObjectExportManager"/>: the core formats
    /// are found, keys are matched ignoring case, a registered format joins in its order and
    /// leaves again.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectExportManager
    {
        /// <summary>
        /// A format as a plugin would ship it.
        /// </summary>
        private sealed class XmlFormat : IObjectExportFormat
        {
            public string Key => "XML";
            public string Extension => "xml";
            public string Label => "xml.label";
            public string Description => "xml.description";
            public string Content => "xml.content";
            public string Badge => "XML";
            public TypeColorTile Color => TypeColorTile.Info;
            public IIcon Icon => new IconFileCode();
            public int Order => 15;

            public IResponse Write(ObjectExportRecord record, IRequest request)
            {
                return ObjectExport.Attachment([], "application/xml", ObjectExport.FileName(record, Extension));
            }
        }

        /// <summary>
        /// The core ships PDF, JSON and CSV in this order.
        /// </summary>
        [Fact]
        public void Formats_AreTheCoreFormatsInOrder()
        {
            var keys = CoreHub.ObjectExportManager.Formats
                .Where(x => x.GetType().Assembly == typeof(CoreHub).Assembly)
                .Select(x => x.Key)
                .ToList();

            Assert.Equal(["pdf", "json", "csv"], keys);
        }

        /// <summary>
        /// A key is matched ignoring case and surrounding blanks; an unknown or blank key names
        /// no format.
        /// </summary>
        [Theory]
        [InlineData("pdf", typeof(PdfObjectExportFormat))]
        [InlineData(" JSON ", typeof(JsonObjectExportFormat))]
        [InlineData("Csv", typeof(CsvObjectExportFormat))]
        [InlineData("xlsx", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void Get_MatchesTheKey(string key, Type expected)
        {
            Assert.Equal(expected, CoreHub.ObjectExportManager.Get(key)?.GetType());
        }

        /// <summary>
        /// A registered format is found under its normalized key, takes its place by order and
        /// leaves on unregister.
        /// </summary>
        [Fact]
        public void Register_AddsAFormatInItsOrder()
        {
            var manager = CoreHub.ObjectExportManager;
            var version = manager.Version;

            manager.Register(new XmlFormat());

            try
            {
                Assert.IsType<XmlFormat>(manager.Get("xml"));
                Assert.NotEqual(version, manager.Version);
                Assert.Equal(["pdf", "XML", "json", "csv"], manager.Formats.Select(x => x.Key).Where(x => x is "pdf" or "XML" or "json" or "csv").ToList());
            }
            finally
            {
                Assert.True(manager.Unregister("xml"));
            }

            Assert.Null(manager.Get("xml"));
        }

        /// <summary>
        /// A format without a key is refused.
        /// </summary>
        [Fact]
        public void Register_RefusesABlankKey()
        {
            var format = new BlankFormat();

            Assert.Throws<ArgumentException>(() => CoreHub.ObjectExportManager.Register(format));
        }

        /// <summary>
        /// A format with a blank key.
        /// </summary>
        private sealed class BlankFormat : IObjectExportFormat
        {
            public string Key => " ";
            public string Extension => "bin";
            public string Label => null;
            public string Description => null;
            public string Content => null;
            public string Badge => null;
            public TypeColorTile Color => TypeColorTile.Default;
            public IIcon Icon => null;
            public int Order => 0;

            public IResponse Write(ObjectExportRecord record, IRequest request) => null;
        }
    }
}
