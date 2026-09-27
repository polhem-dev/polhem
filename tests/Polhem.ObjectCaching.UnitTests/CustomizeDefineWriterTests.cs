using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Settings;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// Behavior tests of the file-backed <see cref="CustomizeDefineWriter"/>: it writes the tenant's
    /// <c>PluginSettings.xml</c> under <c>{CustomizePath}/{customizeId}/</c>, evicts that tenant's cached copy so the
    /// next read sees the write, leaves other tenants alone, and refuses to write when customization is not configured.
    /// </summary>
    public sealed class CustomizeDefineWriterTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"polhem-custwriter-{Guid.NewGuid():N}");
        private readonly string _customizeId = "cust" + Guid.NewGuid().ToString("N");
        private readonly PathOptions _paths;
        private readonly CacheContainerProvider _provider;

        public CustomizeDefineWriterTests()
        {
            Directory.CreateDirectory(_root);
            _paths = new PathOptions { DefinePath = "/tmp/base", CustomizePath = _root };
            _provider = new CacheContainerProvider(_paths);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch (IOException) { /* best effort */ }
        }

        private static PluginSettings Settings(string progId, string pluginType)
        {
            var settings = new PluginSettings();
            settings.Items!.Add(progId).Plugins!.Add(pluginType, PluginStage.BeforeSave);
            return settings;
        }

        [Fact]
        [DisplayName("SaveCustomizePluginSettings writes the tenant's PluginSettings.xml where the customize reader looks")]
        public void Save_WritesTenantFile()
        {
            var writer = new CustomizeDefineWriter(_provider, _paths);

            writer.SaveCustomizePluginSettings(_customizeId, Settings("Order", "MyErp.OrderPlugin, MyErp"));

            string expectedPath = new CustomizeOnlyPathOptions(_root, _customizeId).GetPluginSettingsFilePath();
            Assert.True(File.Exists(expectedPath), $"Expected {expectedPath} to be written.");
            var stored = new CustomizeDefineReader(new CacheContainerProvider(_paths), _paths)
                .GetCustomizePluginSettings(_customizeId);
            var binding = Assert.Single(stored!.GetPluginBindings("Order"));
            Assert.Equal("MyErp.OrderPlugin, MyErp", binding.Type);
            Assert.Equal(PluginStage.BeforeSave, binding.Stage);
        }

        [Fact]
        [DisplayName("SaveCustomizePluginSettings evicts the tenant's cached copy so the next read returns what was written")]
        public void Save_AfterCachedRead_NextReadSeesWrite()
        {
            var reader = new CustomizeDefineReader(_provider, _paths);
            var writer = new CustomizeDefineWriter(_provider, _paths);
            writer.SaveCustomizePluginSettings(_customizeId, Settings("Order", "First.Plugin, First"));
            Assert.Equal("First.Plugin, First", Assert.Single(reader.GetCustomizePluginSettings(_customizeId)!.GetPluginBindings("Order")).Type);

            writer.SaveCustomizePluginSettings(_customizeId, Settings("Order", "Second.Plugin, Second"));

            Assert.Equal("Second.Plugin, Second", Assert.Single(reader.GetCustomizePluginSettings(_customizeId)!.GetPluginBindings("Order")).Type);
        }

        [Fact]
        [DisplayName("SaveCustomizePluginSettings for one tenant does not change another tenant's settings")]
        public void Save_OneTenant_OtherTenantUnaffected()
        {
            string other = "cust" + Guid.NewGuid().ToString("N");
            var reader = new CustomizeDefineReader(_provider, _paths);
            var writer = new CustomizeDefineWriter(_provider, _paths);
            writer.SaveCustomizePluginSettings(other, Settings("Order", "Other.Plugin, Other"));

            writer.SaveCustomizePluginSettings(_customizeId, Settings("Order", "Mine.Plugin, Mine"));

            Assert.Equal("Other.Plugin, Other", Assert.Single(reader.GetCustomizePluginSettings(other)!.GetPluginBindings("Order")).Type);
        }

        [Fact]
        [DisplayName("SaveCustomizePluginSettings throws InvalidOperationException when CustomizePath is not configured")]
        public void Save_NoCustomizePath_Throws()
        {
            var paths = new PathOptions { DefinePath = "/tmp/base", CustomizePath = string.Empty };
            var writer = new CustomizeDefineWriter(new CacheContainerProvider(paths), paths);

            var ex = Assert.Throws<InvalidOperationException>(
                () => writer.SaveCustomizePluginSettings(_customizeId, new PluginSettings()));
            Assert.Contains("CustomizePath", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("SaveCustomizePluginSettings rejects a blank customizeId")]
        public void Save_BlankCustomizeId_Throws(string customizeId)
        {
            var writer = new CustomizeDefineWriter(_provider, _paths);

            Assert.Throws<ArgumentException>(() => writer.SaveCustomizePluginSettings(customizeId, new PluginSettings()));
        }

        [Fact]
        [DisplayName("SaveCustomizePluginSettings rejects null settings")]
        public void Save_NullSettings_Throws()
        {
            var writer = new CustomizeDefineWriter(_provider, _paths);

            Assert.Throws<ArgumentNullException>(() => writer.SaveCustomizePluginSettings(_customizeId, null!));
        }
    }
}
