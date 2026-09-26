using System.ComponentModel;
using System.Text.Json;
using Polhem.LoadTests.Caching;
using Polhem.LoadTests.Reporting;
using Polhem.LoadTests.Running;
using Polhem.LoadTests.Statistics;

namespace Polhem.LoadTests.UnitTests
{
    /// <summary>
    /// Tests for <see cref="ReportWriter"/>.
    /// </summary>
    public class ReportWriterTests : IDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(), "polhem-loadtest-report-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_directory)) { Directory.Delete(_directory, recursive: true); }
            GC.SuppressFinalize(this);
        }

        private static RunReport CreateReport(
            IReadOnlyDictionary<string, long>? errors = null,
            IReadOnlyDictionary<string, string>? samples = null,
            string mode = "Local")
        {
            var result = new ScenarioResult(
                "GetList",
                LatencyStatistics.FromMilliseconds([1, 2, 3, 4, 5]),
                successCount: 5,
                errors ?? new Dictionary<string, long>(StringComparer.Ordinal),
                samples ?? new Dictionary<string, string>(StringComparer.Ordinal),
                TimeSpan.FromSeconds(10));

            var metadata = new RunMetadata
            {
                Version = "4.29.0+abc123",
                Provider = "SQLServer",
                Mode = mode,
                ProtectionLevel = "Encrypted",
                OperatingSystem = "TestOS",
                ProcessorCount = 8,
                VirtualUsers = 4,
                WarmupSeconds = 2,
                DurationSeconds = 10,
                LoadModel = "Closed",
                TokenStrategy = "PerUser",
                SeededRows = 1000,
                DroppedBindings = ["Order.BusinessObject"],
            };

            return new RunReport(metadata, [result], new CacheCounters(9, 1, 3, 0), [50, 95]);
        }

        private string Write(RunReport report, string extension)
        {
            var path = Path.Combine(_directory, "report" + extension);
            if (extension == ".md") { ReportWriter.WriteMarkdown(report, path); }
            else { ReportWriter.WriteJson(report, path); }
            return File.ReadAllText(path);
        }

        [Fact]
        [DisplayName("WriteMarkdown includes the metadata needed to interpret the numbers")]
        public void WriteMarkdown_IncludesMetadata()
        {
            var text = Write(CreateReport(), ".md");

            Assert.Contains("4.29.0+abc123", text, StringComparison.Ordinal);
            Assert.Contains("SQLServer", text, StringComparison.Ordinal);
            Assert.Contains("Encrypted", text, StringComparison.Ordinal);
            Assert.Contains("Closed model", text, StringComparison.Ordinal);
            Assert.Contains("8 logical cores", text, StringComparison.Ordinal);
            Assert.Contains("PerUser", text, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("WriteMarkdown lists dropped bindings, so readers know the framework implementation was measured")]
        public void WriteMarkdown_ListsDroppedBindings()
        {
            Assert.Contains("Order.BusinessObject", Write(CreateReport(), ".md"),
                StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("WriteMarkdown omits the errors section when there are no errors")]
        public void WriteMarkdown_NoErrors_OmitsErrorSection()
        {
            Assert.DoesNotContain("## Errors", Write(CreateReport(), ".md"),
                StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("WriteMarkdown lists the type, count and sample message of each error")]
        public void WriteMarkdown_WithErrors_ListsTypeCountAndSample()
        {
            var report = CreateReport(
                new Dictionary<string, long>(StringComparer.Ordinal) { ["TimeoutException"] = 3 },
                new Dictionary<string, string>(StringComparer.Ordinal) { ["TimeoutException"] = "took too long" });

            var text = Write(report, ".md");

            Assert.Contains("## Errors", text, StringComparison.Ordinal);
            Assert.Contains("TimeoutException", text, StringComparison.Ordinal);
            Assert.Contains("took too long", text, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("WriteMarkdown escapes table pipes in error messages so the layout stays intact")]
        public void WriteMarkdown_EscapesPipesInMessages()
        {
            var report = CreateReport(
                new Dictionary<string, long>(StringComparer.Ordinal) { ["DbException"] = 1 },
                new Dictionary<string, string>(StringComparer.Ordinal) { ["DbException"] = "a | b" });

            Assert.Contains(@"a \| b", Write(report, ".md"), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("WriteJson output parses and includes the metadata and percentiles")]
        public void WriteJson_IsParseableAndComplete()
        {
            using var document = JsonDocument.Parse(Write(CreateReport(), ".json"));
            var root = document.RootElement;

            Assert.Equal("SQLServer", root.GetProperty("Metadata").GetProperty("Provider").GetString());
            Assert.Equal(4, root.GetProperty("Metadata").GetProperty("VirtualUsers").GetInt32());

            var scenario = root.GetProperty("Scenarios")[0];
            Assert.Equal("GetList", scenario.GetProperty("Name").GetString());
            Assert.Equal(5, scenario.GetProperty("SuccessCount").GetInt64());

            var percentiles = scenario.GetProperty("Latencies").GetProperty("Percentiles");
            Assert.Equal(3, percentiles.GetProperty("50").GetDouble());
            Assert.Equal(5, percentiles.GetProperty("95").GetDouble());
        }

        [Fact]
        [DisplayName("WriteJson includes the cache hit rate for comparison across runs")]
        public void WriteJson_IncludesCacheCounters()
        {
            using var document = JsonDocument.Parse(Write(CreateReport(), ".json"));

            var cache = document.RootElement.GetProperty("Cache");
            Assert.Equal(10, cache.GetProperty("Reads").GetInt64());
            Assert.Equal(0.9, cache.GetProperty("HitRate").GetDouble(), 3);
        }

        [Fact]
        [DisplayName("WriteMarkdown in Remote mode marks the cache as not observed instead of reporting a 0% hit rate")]
        public void WriteMarkdown_RemoteMode_MarksCacheNotObserved()
        {
            var text = Write(CreateReport(mode: "Remote"), ".md");

            // Reporting a 0% hit rate here would be a lie: the counters simply cannot see the
            // server's cache from this process.
            Assert.Contains("Not observed", text, StringComparison.Ordinal);
            Assert.DoesNotContain("| Hit rate |", text, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("WriteMarkdown in Local mode includes the cache numbers")]
        public void WriteMarkdown_LocalMode_IncludesCacheTable()
        {
            var text = Write(CreateReport(), ".md");

            Assert.Contains("| Hit rate |", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Not observed", text, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("WriteJson carries the CacheObserved flag for programmatic use")]
        public void WriteJson_CarriesCacheObservedFlag()
        {
            using var local = JsonDocument.Parse(Write(CreateReport(), ".json"));
            Assert.True(local.RootElement.GetProperty("CacheObserved").GetBoolean());

            using var remote = JsonDocument.Parse(Write(CreateReport(mode: "Remote"), ".json"));
            Assert.False(remote.RootElement.GetProperty("CacheObserved").GetBoolean());
        }

        [Fact]
        [DisplayName("WriteMarkdown creates the output directory when it does not exist")]
        public void Write_CreatesMissingDirectory()
        {
            var nested = Path.Combine(_directory, "a", "b", "report.md");

            ReportWriter.WriteMarkdown(CreateReport(), nested);

            Assert.True(File.Exists(nested));
        }
    }
}
