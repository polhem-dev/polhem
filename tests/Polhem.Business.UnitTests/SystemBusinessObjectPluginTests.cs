using System.ComponentModel;
using Polhem.Core.Exceptions;
using Polhem.Core.Serialization;
using Polhem.Business.Form;
using Polhem.Business.System;
using Polhem.Definition;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// The plugin maintenance API of <see cref="SystemBusinessObject"/>: the LocalOnly defense, validation on save,
    /// an empty string meaning clear, and writes delegated to <see cref="ICustomizeDefineWriter"/>.
    /// </summary>
    public class SystemBusinessObjectPluginTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SystemBusinessObjectPluginTests(SharedDbFixture fx)
        {
            _fx = fx;
        }

        private static string SamplePluginFqn =>
            $"{typeof(SamplePlugin).FullName}, {typeof(SamplePlugin).Assembly.GetName().Name}";

        private static string NoStagePluginFqn =>
            $"{typeof(NoStagePlugin).FullName}, {typeof(NoStagePlugin).Assembly.GetName().Name}";

        private SystemBusinessObject CreateBo(SpyWriter writer, SpyReader? reader = null, bool isLocalCall = true)
        {
            var ctx = TestBusinessObjectContext.CreateWithOverrides(_fx,
                (typeof(ICustomizeDefineWriter), writer),
                (typeof(ICustomizeDefineReader), reader ?? new SpyReader()));
            return new SystemBusinessObject(ctx, TestSessionFactory.CreateAccessToken(_fx), SysProgIds.System, isLocalCall);
        }

        private static string XmlFor(params (string Type, PluginStage Stage)[] plugins)
        {
            var settings = new PluginSettings();
            var program = settings.Items!.Add("Order");
            foreach (var (type, stage) in plugins)
                program.Plugins!.Add(type, stage);
            return XmlCodec.Serialize(settings);
        }

        /// <summary><see cref="SamplePlugin"/> overrides BeforeSave, so the declaration matches the override.</summary>
        private static (string, PluginStage) Sample => (SamplePluginFqn, PluginStage.BeforeSave);

        [Fact]
        [DisplayName("A remote call is rejected (the second line of defense beyond the attribute)")]
        public void SaveCustomizePluginSettings_RemoteCall_Throws()
        {
            var bo = CreateBo(new SpyWriter(), isLocalCall: false);

            Assert.Throws<NotSupportedException>(() =>
                bo.SaveCustomizePluginSettings(new SaveCustomizePluginSettingsArgs
                {
                    CustomizeId = "acme",
                    Xml = XmlFor(Sample),
                }));
        }

        [Fact]
        [DisplayName("A remote read call is rejected as well")]
        public void GetCustomizePluginSettings_RemoteCall_Throws()
        {
            var bo = CreateBo(new SpyWriter(), isLocalCall: false);

            Assert.Throws<NotSupportedException>(() =>
                bo.GetCustomizePluginSettings(new GetCustomizePluginSettingsArgs { CustomizeId = "acme" }));
        }

        [Fact]
        [DisplayName("A missing customize ID is rejected (an empty ID means the packaged layer, not a tenant customization)")]
        public void SaveCustomizePluginSettings_EmptyCustomizeId_Throws()
        {
            var bo = CreateBo(new SpyWriter());

            Assert.Throws<UserMessageException>(() =>
                bo.SaveCustomizePluginSettings(new SaveCustomizePluginSettingsArgs { CustomizeId = "", Xml = "" }));
        }

        [Fact]
        [DisplayName("A successful save delegates the write to the writer and reports the binding count")]
        public void SaveCustomizePluginSettings_Valid_WritesAndReportsCount()
        {
            var writer = new SpyWriter();
            var bo = CreateBo(writer);

            var result = bo.SaveCustomizePluginSettings(new SaveCustomizePluginSettingsArgs
            {
                CustomizeId = "acme",
                Xml = XmlFor(Sample),
            });

            Assert.Equal(1, result.PluginCount);
            Assert.Equal("acme", writer.LastCustomizeId);
            Assert.Equal([new PluginBinding(SamplePluginFqn, PluginStage.BeforeSave)],
                writer.LastSettings!.GetPluginBindings("Order"));
        }

        [Fact]
        [DisplayName("Empty XML clears the tenant's bindings without a separate API")]
        public void SaveCustomizePluginSettings_EmptyXml_ClearsBindings()
        {
            var writer = new SpyWriter();
            var bo = CreateBo(writer);

            var result = bo.SaveCustomizePluginSettings(new SaveCustomizePluginSettingsArgs
            {
                CustomizeId = "acme",
                Xml = string.Empty,
            });

            Assert.Equal(0, result.PluginCount);
            Assert.NotNull(writer.LastSettings);
            Assert.Empty(writer.LastSettings!.GetPluginBindings("Order"));
        }

        [Fact]
        [DisplayName("A type that cannot be loaded is rejected and nothing is written")]
        public void SaveCustomizePluginSettings_UnloadableType_RejectsWithoutWriting()
        {
            var writer = new SpyWriter();
            var bo = CreateBo(writer);

            var ex = Assert.Throws<UserMessageException>(() =>
                bo.SaveCustomizePluginSettings(new SaveCustomizePluginSettingsArgs
                {
                    CustomizeId = "acme",
                    Xml = XmlFor(("Nope.Missing, Nope", PluginStage.BeforeSave)),
                }));

            Assert.Contains("Nope.Missing, Nope", ex.Message, StringComparison.Ordinal);
            Assert.Null(writer.LastSettings);
        }

        [Fact]
        [DisplayName("A plugin that overrides no stage is rejected (binding it would do nothing)")]
        public void SaveCustomizePluginSettings_PluginWithNoStage_Rejects()
        {
            var writer = new SpyWriter();
            var bo = CreateBo(writer);

            var ex = Assert.Throws<UserMessageException>(() =>
                bo.SaveCustomizePluginSettings(new SaveCustomizePluginSettingsArgs
                {
                    CustomizeId = "acme",
                    Xml = XmlFor((NoStagePluginFqn, PluginStage.BeforeSave)),
                }));

            Assert.Contains("overrides no stage", ex.Message, StringComparison.Ordinal);
            Assert.Null(writer.LastSettings);
        }

        [Fact]
        [DisplayName("One invalid entry rejects the whole definition, and the valid entry is not written either")]
        public void SaveCustomizePluginSettings_OneBadEntry_RejectsWholeDefinition()
        {
            var writer = new SpyWriter();
            var bo = CreateBo(writer);

            Assert.Throws<UserMessageException>(() =>
                bo.SaveCustomizePluginSettings(new SaveCustomizePluginSettingsArgs
                {
                    CustomizeId = "acme",
                    Xml = XmlFor(Sample, ("Nope.Missing, Nope", PluginStage.BeforeSave)),
                }));

            Assert.Null(writer.LastSettings);
        }

        [Fact]
        [DisplayName("Malformed XML produces a readable message instead of the raw serialization exception")]
        public void SaveCustomizePluginSettings_MalformedXml_ThrowsUserMessage()
        {
            var bo = CreateBo(new SpyWriter());

            Assert.Throws<UserMessageException>(() =>
                bo.SaveCustomizePluginSettings(new SaveCustomizePluginSettingsArgs
                {
                    CustomizeId = "acme",
                    Xml = "<PluginSettings><broken>",
                }));
        }

        [Fact]
        [DisplayName("A tenant without a customization reads back an empty string, so the caller starts from a blank definition")]
        public void GetCustomizePluginSettings_NoOverride_ReturnsEmptyString()
        {
            var bo = CreateBo(new SpyWriter(), new SpyReader { Settings = null });

            var result = bo.GetCustomizePluginSettings(new GetCustomizePluginSettingsArgs { CustomizeId = "acme" });

            Assert.Equal(string.Empty, result.Xml);
        }

        [Fact]
        [DisplayName("The XML read back restores the same bindings")]
        public void GetCustomizePluginSettings_WithOverride_RoundTrips()
        {
            var stored = new PluginSettings();
            stored.Items!.Add("Order").Plugins!.Add(SamplePluginFqn, PluginStage.BeforeSave);
            var bo = CreateBo(new SpyWriter(), new SpyReader { Settings = stored });

            var result = bo.GetCustomizePluginSettings(new GetCustomizePluginSettingsArgs { CustomizeId = "acme" });

            var restored = XmlCodec.Deserialize<PluginSettings>(result.Xml)!;
            Assert.Equal([new PluginBinding(SamplePluginFqn, PluginStage.BeforeSave)],
                restored.GetPluginBindings("Order"));
        }

        // ---- Test doubles ----

        public sealed class SamplePlugin : FormBusinessPlugin
        {
            public SamplePlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId) { }

            public override void BeforeSave(SaveContext context) { }
        }

        /// <summary>Inherits correctly but overrides no stage, so it would never run even when bound.</summary>
        public sealed class NoStagePlugin : FormBusinessPlugin
        {
            public NoStagePlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId) { }
        }

        private sealed class SpyWriter : ICustomizeDefineWriter
        {
            public string? LastCustomizeId { get; private set; }

            public PluginSettings? LastSettings { get; private set; }

            public void SaveCustomizePluginSettings(string customizeId, PluginSettings settings)
            {
                LastCustomizeId = customizeId;
                LastSettings = settings;
            }
        }

        private sealed class SpyReader : ICustomizeDefineReader
        {
            public PluginSettings? Settings { get; set; }

            public PluginSettings? GetCustomizePluginSettings(string customizeId) => Settings;

            public Definition.Language.LanguageResource? GetCustomizeLanguage(string customizeId, string lang, string ns) => null;
            public ProgramSettings? GetCustomizeProgramSettings(string customizeId) => null;
            public MenuSettings? GetCustomizeMenuSettings(string customizeId) => null;
            public Definition.Layouts.FormLayout? GetCustomizeFormLayout(string customizeId, string layoutId) => null;
        }
    }
}
