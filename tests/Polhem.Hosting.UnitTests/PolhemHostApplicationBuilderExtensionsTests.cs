using System.ComponentModel;
using Microsoft.Extensions.Hosting;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.JsonRpc.Payload;

namespace Polhem.Hosting.UnitTests
{
    // NOTE: AddPolhemBackend initializes the process-wide SysInfo. The materialized default SystemSettings has debug
    // mode off and no allowed type namespaces, which is the state SysInfo starts in, so other tests running in
    // parallel see no change apart from SysInfo.Version, which none of them reads.
    public sealed class PolhemHostApplicationBuilderExtensionsTests : IDisposable
    {
        private readonly string _root = Directory.CreateTempSubdirectory("polhem-backend-").FullName;

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch (IOException) { /* best effort */ }
        }

        // A definition folder with the framework defaults, its master key kept in a file the framework creates, so the
        // tests never set the POLHEM_MASTER_KEY environment variable of the process.
        private string PrepareDefine()
        {
            var definePath = Path.Combine(_root, "Define");
            Defaults.MaterializeTo(definePath);
            var paths = new PathOptions { DefinePath = definePath };
            var settings = SystemSettingsLoader.Load(paths);
            settings.BackendConfiguration.SecurityKeySettings.MasterKeySource = new MasterKeySource
            {
                Type = MasterKeySourceType.File,
                Value = "Master.key",
            };
            XmlCodec.SerializeToFile(settings, paths.GetSystemSettingsFilePath());
            return definePath;
        }

        private static HostApplicationBuilder CreateBuilder() => Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        [Fact]
        [DisplayName("AddPolhemBackend registers the framework and the payload options with the paths it returns")]
        public void AddPolhemBackend_ExplicitPaths_RegistersFrameworkWithThem()
        {
            var definePath = PrepareDefine();
            var customizePath = Path.Combine(_root, "Customize");
            var builder = CreateBuilder();

            var paths = builder.AddPolhemBackend(o =>
            {
                o.DefinePath = definePath;
                o.CustomizePath = customizePath;
                o.AutoCreateMasterKey = true;
            });

            Assert.Equal(definePath, paths.DefinePath);
            Assert.Equal(customizePath, paths.CustomizePath);
            Assert.Same(paths, builder.Services.Single(d => d.ServiceType == typeof(PathOptions)).ImplementationInstance);
            Assert.Contains(builder.Services, d => d.ServiceType == typeof(PayloadOptions));
            Assert.True(File.Exists(Path.Combine(definePath, "Master.key")));
        }

        [Fact]
        [DisplayName("AddPolhemBackend writes a framework default the definition folder does not have yet")]
        public void AddPolhemBackend_MissingDefault_IsMaterialized()
        {
            var definePath = PrepareDefine();
            var unitSettings = Path.Combine(definePath, "UnitSettings.xml");
            File.Delete(unitSettings);

            CreateBuilder().AddPolhemBackend(o =>
            {
                o.DefinePath = definePath;
                o.AutoCreateMasterKey = true;
            });

            Assert.True(File.Exists(unitSettings));
        }

        [Fact]
        [DisplayName("AddPolhemBackend with MaterializeDefaults off leaves a missing default missing")]
        public void AddPolhemBackend_MaterializeDefaultsOff_WritesNothing()
        {
            var definePath = PrepareDefine();
            var unitSettings = Path.Combine(definePath, "UnitSettings.xml");
            File.Delete(unitSettings);

            CreateBuilder().AddPolhemBackend(o =>
            {
                o.DefinePath = definePath;
                o.MaterializeDefaults = false;
                o.AutoCreateMasterKey = true;
            });

            Assert.False(File.Exists(unitSettings));
        }

        [Fact]
        [DisplayName("FindDefinePath returns the Define folder with a SystemSettings.xml above the start directory")]
        public void FindDefinePath_DefineInAncestor_ReturnsIt()
        {
            var define = Directory.CreateDirectory(Path.Combine(_root, "app", "Define")).FullName;
            File.WriteAllText(Path.Combine(define, "SystemSettings.xml"), "<SystemSettings />");
            var start = Directory.CreateDirectory(Path.Combine(_root, "app", "src", "Server", "bin")).FullName;

            Assert.Equal(define, PolhemHostApplicationBuilderExtensions.FindDefinePath(start));
        }

        [Fact]
        [DisplayName("FindDefinePath skips a Define folder without a SystemSettings.xml")]
        public void FindDefinePath_DefineWithoutSettings_IsSkipped()
        {
            Directory.CreateDirectory(Path.Combine(_root, "app", "bin", "Define"));
            var start = Directory.CreateDirectory(Path.Combine(_root, "app", "bin")).FullName;

            var ex = Record.Exception(() => PolhemHostApplicationBuilderExtensions.FindDefinePath(start));

            Assert.IsType<InvalidOperationException>(ex);
        }
    }
}
