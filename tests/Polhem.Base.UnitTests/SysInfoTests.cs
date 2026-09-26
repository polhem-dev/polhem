using System.ComponentModel;
using Polhem.Base.Tracing;

namespace Polhem.Base.UnitTests
{
    /// <summary>
    /// Tests for the SysInfo properties and initialization that are not security related.
    /// </summary>
    [Collection("SysInfoStatic")]
    public class SysInfoTests : IDisposable
    {
        private readonly string _originalVersion;
        private readonly bool _originalDebug;
        private readonly bool _originalToolMode;
        private readonly bool _originalSingleFile;
        private readonly ITraceListener? _originalListener;
        private readonly List<string> _originalNamespaces;

        private sealed class FakeConfig : ISysInfoConfiguration
        {
            public string Version { get; set; } = string.Empty;
            public bool IsDebugMode { get; set; }
            public string AllowedTypeNamespaces { get; set; } = string.Empty;
        }

        private sealed class FakeListener : ITraceListener
        {
            public TraceContext TraceStart(TraceLayers layer, string detail = "",
                string category = "", object? tag = null, string name = "")
                => throw new NotImplementedException();

            public void TraceEnd(TraceContext ctx, TraceStatus status = TraceStatus.Ok, string? detail = null)
            { }

            public void TraceWrite(TraceLayers layer, string detail = "", TraceStatus status = TraceStatus.Ok,
                string category = "", object? tag = null, string name = "")
            { }
        }

        public SysInfoTests()
        {
            _originalVersion = SysInfo.Version;
            _originalDebug = SysInfo.IsDebugMode;
            _originalToolMode = SysInfo.IsToolMode;
            _originalSingleFile = SysInfo.IsSingleFile;
            _originalListener = SysInfo.TraceListener;
            _originalNamespaces = SysInfo.AllowedTypeNamespaces.ToList();
        }

        public void Dispose()
        {
            SysInfo.Version = _originalVersion;
            SysInfo.IsDebugMode = _originalDebug;
            SysInfo.IsToolMode = _originalToolMode;
            SysInfo.IsSingleFile = _originalSingleFile;
            SysInfo.TraceListener = _originalListener;
            // Restore namespaces via Initialize with the original custom list (none here, defaults are enough).
            SysInfo.Initialize(new FakeConfig
            {
                Version = _originalVersion,
                IsDebugMode = _originalDebug,
                AllowedTypeNamespaces = string.Join('|', _originalNamespaces)
            });
            GC.SuppressFinalize(this);
        }

        [Fact]
        [DisplayName("Version is read-write")]
        public void Version_IsReadWrite()
        {
            SysInfo.Version = "99.9.9";
            Assert.Equal("99.9.9", SysInfo.Version);
        }

        [Fact]
        [DisplayName("TraceEnabled reflects whether a TraceListener is set")]
        public void TraceListener_AffectsTraceEnabled()
        {
            SysInfo.TraceListener = null;
            Assert.False(SysInfo.TraceEnabled);

            SysInfo.TraceListener = new FakeListener();
            Assert.True(SysInfo.TraceEnabled);
        }

        [Fact]
        [DisplayName("The IsDebugMode / IsToolMode / IsSingleFile flags are read-write")]
        public void ModeFlags_AreReadWrite()
        {
            SysInfo.IsDebugMode = true;
            SysInfo.IsToolMode = true;
            SysInfo.IsSingleFile = true;

            Assert.True(SysInfo.IsDebugMode);
            Assert.True(SysInfo.IsToolMode);
            Assert.True(SysInfo.IsSingleFile);

            SysInfo.IsDebugMode = false;
            SysInfo.IsToolMode = false;
            SysInfo.IsSingleFile = false;

            Assert.False(SysInfo.IsDebugMode);
            Assert.False(SysInfo.IsToolMode);
            Assert.False(SysInfo.IsSingleFile);
        }

        [Fact]
        [DisplayName("Initialize applies Version and IsDebugMode and keeps the default namespaces")]
        public void Initialize_AppliesVersionDebugAndDefaultNamespaces()
        {
            SysInfo.Initialize(new FakeConfig
            {
                Version = "5.0.0",
                IsDebugMode = true,
                AllowedTypeNamespaces = string.Empty
            });

            Assert.Equal("5.0.0", SysInfo.Version);
            Assert.True(SysInfo.IsDebugMode);

            Assert.True(SysInfo.IsTypeNameAllowed("Polhem.Base.SomeClass"));
            Assert.True(SysInfo.IsTypeNameAllowed("Polhem.Definition.Foo"));
            Assert.True(SysInfo.IsTypeNameAllowed("Polhem.Api.Contracts.Bar"));
            Assert.True(SysInfo.IsTypeNameAllowed("Polhem.Api.Core.Baz"));
            Assert.True(SysInfo.IsTypeNameAllowed("Polhem.Business.Qux"));
        }

        [Theory]
        [InlineData("MyApp.Dto|MyApp.Models")]
        [InlineData("  MyApp.Dto  |  MyApp.Models  ")]
        [InlineData("MyApp.Dto.|MyApp.Models.")]
        [InlineData("||MyApp.Dto|MyApp.Models||")]
        [DisplayName("Initialize parses custom namespaces, ignoring whitespace, trailing dots and empty entries")]
        public void Initialize_ParsesCustomNamespaces(string raw)
        {
            SysInfo.Initialize(new FakeConfig
            {
                Version = "1.0",
                IsDebugMode = false,
                AllowedTypeNamespaces = raw
            });

            Assert.True(SysInfo.IsTypeNameAllowed("MyApp.Dto.Order"));
            Assert.True(SysInfo.IsTypeNameAllowed("MyApp.Models.Product"));
            Assert.False(SysInfo.IsTypeNameAllowed("Other.Namespace.Thing"));
        }

        [Fact]
        [DisplayName("Initialize does not create duplicate entries for repeated namespaces")]
        public void Initialize_DuplicateNamespaces_AreDeduplicated()
        {
            SysInfo.Initialize(new FakeConfig
            {
                Version = "1.0",
                IsDebugMode = false,
                AllowedTypeNamespaces = "Polhem.Base|Polhem.Base|MyApp.Dto|MyApp.Dto"
            });

            var list = SysInfo.AllowedTypeNamespaces;
            Assert.Equal(list.Count, list.Distinct().Count());
            Assert.Contains("MyApp.Dto", list);
        }
    }
}
