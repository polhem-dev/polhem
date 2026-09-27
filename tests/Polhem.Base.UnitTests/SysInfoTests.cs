using System.ComponentModel;

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
        private readonly List<string> _originalNamespaces;

        private sealed class FakeConfig : ISysInfoConfiguration
        {
            public string Version { get; set; } = string.Empty;
            public bool IsDebugMode { get; set; }
            public string AllowedTypeNamespaces { get; set; } = string.Empty;
        }

        public SysInfoTests()
        {
            _originalVersion = SysInfo.Version;
            _originalDebug = SysInfo.IsDebugMode;
            _originalNamespaces = SysInfo.AllowedTypeNamespaces.ToList();
        }

        public void Dispose()
        {
            SysInfo.Version = _originalVersion;
            SysInfo.IsDebugMode = _originalDebug;
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
        [DisplayName("The IsDebugMode flag is read-write")]
        public void IsDebugMode_IsReadWrite()
        {
            SysInfo.IsDebugMode = true;
            Assert.True(SysInfo.IsDebugMode);

            SysInfo.IsDebugMode = false;
            Assert.False(SysInfo.IsDebugMode);
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
