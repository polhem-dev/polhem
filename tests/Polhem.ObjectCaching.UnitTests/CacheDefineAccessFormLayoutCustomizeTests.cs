using System.ComponentModel;
using Polhem.Base.Serialization;
using Polhem.Definition;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// Tests of the whole-file override in <see cref="CacheDefineAccess.GetFormLayout(string, string)"/>:
    /// a customized file wins when it exists, otherwise the base file is returned; an empty customizeId or no
    /// reader short-circuits to the base file (the reader is never called).
    /// </summary>
    public sealed class CacheDefineAccessFormLayoutCustomizeTests : IDisposable
    {
        private readonly string _baseRoot;
        private const string LayoutId = "EmployeeDefault";

        public CacheDefineAccessFormLayoutCustomizeTests()
        {
            _baseRoot = Path.Combine(Path.GetTempPath(), $"polhem-fl-cust-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_baseRoot);
        }

        public void Dispose()
        {
            try { Directory.Delete(_baseRoot, recursive: true); } catch (IOException) { /* best effort */ }
        }

        private CacheDefineAccess CreateAccess(ICustomizeDefineReader? reader)
        {
            var paths = new PathOptions { DefinePath = _baseRoot };
            // Write a base FormLayout so the base lookup returns a known instance.
            XmlCodec.SerializeToFile(new FormLayout { LayoutId = LayoutId }, paths.GetFormLayoutFilePath(LayoutId));
            var storage = new FileDefineStorage(paths);
            // Unique CachePrefix isolates these cache entries from other parallel tests.
            var cache = new CacheContainerService(storage, paths, Guid.NewGuid().ToString("N"));
            return new CacheDefineAccess(storage, paths, cache, Array.Empty<byte>(), reader);
        }

        [Fact]
        [DisplayName("GetFormLayout returns the customized layout when the customized file exists (whole-file override)")]
        public void GetFormLayout_CustExists_ReturnsCust()
        {
            var custLayout = new FormLayout { LayoutId = LayoutId };
            var reader = new SpyCustomizeReader { FormLayout = custLayout };
            var access = CreateAccess(reader);

            var result = access.GetFormLayout("acme", LayoutId);

            Assert.Same(custLayout, result);
        }

        [Fact]
        [DisplayName("GetFormLayout returns the base layout when the customized file does not exist")]
        public void GetFormLayout_CustMissing_ReturnsBase()
        {
            var reader = new SpyCustomizeReader { FormLayout = null };
            var access = CreateAccess(reader);

            var result = access.GetFormLayout("acme", LayoutId);

            Assert.NotNull(result);
            Assert.Equal(LayoutId, result.LayoutId);
        }

        [Fact]
        [DisplayName("GetFormLayout with an empty customizeId short-circuits to the base layout without calling the reader")]
        public void GetFormLayout_EmptyCustomizeId_ShortCircuits_ReaderNotCalled()
        {
            var reader = new SpyCustomizeReader { FormLayout = new FormLayout { LayoutId = LayoutId } };
            var access = CreateAccess(reader);

            var result = access.GetFormLayout("", LayoutId);

            Assert.Equal(LayoutId, result.LayoutId);
            Assert.Equal(0, reader.GetCustomizeFormLayoutCallCount);
        }

        [Fact]
        [DisplayName("GetFormLayout without an injected reader returns the base layout even with a customizeId (backward compatible)")]
        public void GetFormLayout_NoReader_BehavesAsBase()
        {
            var access = CreateAccess(reader: null);

            var result = access.GetFormLayout("acme", LayoutId);

            Assert.Equal(LayoutId, result.LayoutId);
        }

        private sealed class SpyCustomizeReader : ICustomizeDefineReader
        {
            public FormLayout? FormLayout { get; init; }
            public int GetCustomizeFormLayoutCallCount { get; private set; }

            public FormLayout? GetCustomizeFormLayout(string customizeId, string layoutId)
            {
                GetCustomizeFormLayoutCallCount++;
                return FormLayout;
            }

            public LanguageResource? GetCustomizeLanguage(string customizeId, string lang, string ns) => null;
            public ProgramSettings? GetCustomizeProgramSettings(string customizeId) => null;
            public MenuSettings? GetCustomizeMenuSettings(string customizeId) => null;
            public PluginSettings? GetCustomizePluginSettings(string customizeId) => null;
        }
    }
}
