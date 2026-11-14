using KleeneStar.Core.WebManager;
using KleeneStar.Model;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for the per-request read cache: one read per key and request, a new
    /// read after a write of a type the answer was read from, and nothing shared between
    /// requests.
    /// </summary>
    public class UnitTestRequestReadCache
    {
        /// <summary>
        /// An entity type nothing else in the tests writes, so its version moves only here.
        /// </summary>
        private sealed class Probe;

        /// <summary>
        /// Verifies that a key is read once per request.
        /// </summary>
        [Fact]
        public void ReadsAKeyOncePerRequest()
        {
            var request = new object();
            var reads = 0;

            string Read() => $"value {++reads}";

            var first = RequestReadCache.Get(request, "test", 1, Read, typeof(Probe));
            var second = RequestReadCache.Get(request, "test", 1, Read, typeof(Probe));

            Assert.Equal(1, reads);
            Assert.Same(first, second);
        }

        /// <summary>
        /// Verifies that keys and areas are kept apart, and that an absent answer is kept too.
        /// </summary>
        [Fact]
        public void KeepsKeysAndAreasApart()
        {
            var request = new object();
            var reads = 0;

            string Read() => (++reads) == 1 ? null : $"value {reads}";

            Assert.Null(RequestReadCache.Get(request, "test", 1, Read, typeof(Probe)));
            Assert.Null(RequestReadCache.Get(request, "test", 1, Read, typeof(Probe)));
            Assert.Equal("value 2", RequestReadCache.Get(request, "test", 2, Read, typeof(Probe)));
            Assert.Equal("value 3", RequestReadCache.Get(request, "other", 1, Read, typeof(Probe)));
            Assert.Equal(3, reads);
        }

        /// <summary>
        /// Verifies that a write of a type the answer was read from makes the next call read
        /// again.
        /// </summary>
        [Fact]
        public void ReadsAgainAfterAWrite()
        {
            var request = new object();
            var reads = 0;

            int Read() => ++reads;

            Assert.Equal(1, RequestReadCache.Get(request, "test", 1, Read, typeof(Probe)));

            EntityVersions.Advance([typeof(Probe)]);

            Assert.Equal(2, RequestReadCache.Get(request, "test", 1, Read, typeof(Probe)));
            Assert.Equal(2, RequestReadCache.Get(request, "test", 1, Read, typeof(Probe)));
        }

        /// <summary>
        /// Verifies that two requests do not share an answer.
        /// </summary>
        [Fact]
        public void SharesNothingBetweenRequests()
        {
            var reads = 0;

            int Read() => ++reads;

            Assert.Equal(1, RequestReadCache.Get(new object(), "test", 1, Read, typeof(Probe)));
            Assert.Equal(2, RequestReadCache.Get(new object(), "test", 1, Read, typeof(Probe)));
        }

        /// <summary>
        /// Verifies that a call outside any request reads every time.
        /// </summary>
        [Fact]
        public void ReadsEveryTimeWithoutARequest()
        {
            var reads = 0;

            int Read() => ++reads;

            RequestReadCache.Get("test", 1, Read, typeof(Probe));
            RequestReadCache.Get("test", 1, Read, typeof(Probe));

            Assert.Equal(2, reads);
        }
    }
}
