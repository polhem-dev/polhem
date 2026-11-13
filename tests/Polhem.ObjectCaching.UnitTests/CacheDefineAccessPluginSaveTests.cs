using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// The plugin-binding and permission-model save paths of <see cref="CacheDefineAccess"/>, directly and through
    /// <see cref="CacheDefineAccess.SaveDefine"/>. Each test writes into its own temporary define folder.
    /// </summary>
    public class CacheDefineAccessPluginSaveTests
    {
        [Fact]
        [DisplayName("SavePluginSettings writes PluginSettings.xml with the bindings")]
        public void SavePluginSettings_WritesFile()
        {
            using var temp = new TempDefineFolder();
            var settings = new PluginSettings();
            settings.Items!.Add("P_Save").Plugins!.Add("My.Plugin, My.Assembly", PluginStage.BeforeSave);

            temp.Access.SavePluginSettings(settings);

            var path = temp.Options.GetPluginSettingsFilePath();
            Assert.True(File.Exists(path));
            Assert.Contains("My.Plugin", File.ReadAllText(path), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("SaveDefine with PluginSettings writes PluginSettings.xml")]
        public void SaveDefine_PluginSettings_WritesFile()
        {
            using var temp = new TempDefineFolder();

            temp.Access.SaveDefine(DefineType.PluginSettings, new PluginSettings());

            Assert.True(File.Exists(temp.Options.GetPluginSettingsFilePath()));
        }

        [Fact]
        [DisplayName("SaveDefine with PermissionModels writes PermissionModels.xml with the models")]
        public void SaveDefine_PermissionModels_WritesFile()
        {
            using var temp = new TempDefineFolder();
            var models = new PermissionModels();
            models.Models!.Add("M_Save", "Saved model").Rules!.Add(PermissionActions.Read, ScopeStrategy.Own);

            temp.Access.SaveDefine(DefineType.PermissionModels, models);

            var path = temp.Options.GetPermissionModelsFilePath();
            Assert.True(File.Exists(path));
            Assert.Contains("M_Save", File.ReadAllText(path), StringComparison.Ordinal);
        }

        private sealed class TempDefineFolder : IDisposable
        {
            private readonly string _path;

            public TempDefineFolder()
            {
                _path = Path.Combine(Path.GetTempPath(), $"polhem-pluginsave-{Guid.NewGuid():N}");
                Directory.CreateDirectory(_path);
                Options = new PathOptions { DefinePath = _path };
                Access = new CacheDefineAccess(new FileDefineStorage(Options), Options);
            }

            public PathOptions Options { get; }

            public CacheDefineAccess Access { get; }

            public void Dispose()
            {
                try { Directory.Delete(_path, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }
    }
}
