using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Settings
{
    /// <summary>
    /// Definition-layer behavior of <see cref="PluginSettings"/>: declaration order is execution order, a type may not be
    /// repeated within one program, the stage round-trips with its type, and a missing <c>Stage</c> attribute yields
    /// <see cref="PluginStage.None"/>.
    /// </summary>
    public class PluginSettingsTests
    {
        private static readonly PluginBinding[] s_threeInOrder =
        [
            new("A.First, A", PluginStage.BeforeSave),
            new("A.Second, A", PluginStage.AfterSave),
            new("A.Third, A", PluginStage.AfterDelete),
        ];

        private static readonly PluginBinding[] s_orderChain =
        [
            new("A.CreditLimit, A", PluginStage.BeforeSave),
            new("A.Sync, A", PluginStage.AfterSave),
        ];

        private static readonly PluginBinding[] s_customerChain =
        [
            new("A.Dedupe, A", PluginStage.BeforeSave),
        ];

        [Fact]
        [DisplayName("GetPluginBindings returns bindings in declaration order, which is the execution order")]
        public void GetPluginBindings_ReturnsDeclarationOrder()
        {
            var settings = new PluginSettings();
            var program = settings.Items!.Add("Order");
            program.Plugins!.Add("A.First, A", PluginStage.BeforeSave);
            program.Plugins!.Add("A.Second, A", PluginStage.AfterSave);
            program.Plugins!.Add("A.Third, A", PluginStage.AfterDelete);

            Assert.Equal(s_threeInOrder, settings.GetPluginBindings("Order"));
        }

        [Fact]
        [DisplayName("GetPluginBindings returns an empty collection, not null, for an undeclared progId")]
        public void GetPluginBindings_UnknownProgId_ReturnsEmpty()
        {
            var settings = new PluginSettings();
            settings.Items!.Add("Order").Plugins!.Add("A.First, A", PluginStage.BeforeSave);

            Assert.Empty(settings.GetPluginBindings("Customer"));
            Assert.Empty(new PluginSettings().GetPluginBindings("Order"));
        }

        [Fact]
        [DisplayName("GetPluginBindings returns value copies, so changing one does not touch the cached definition")]
        public void GetPluginBindings_ReturnsValueCopies()
        {
            var settings = new PluginSettings();
            settings.Items!.Add("Order").Plugins!.Add("A.First, A", PluginStage.BeforeSave);

            var binding = Assert.Single(settings.GetPluginBindings("Order"));
            _ = binding with { Stage = PluginStage.AfterDelete };

            Assert.Equal(PluginStage.BeforeSave,
                Assert.Single(settings.GetPluginBindings("Order")).Stage);
        }

        [Fact]
        [DisplayName("Declaring the same type twice within one program is rejected when it is added")]
        public void Plugins_DuplicateType_Throws()
        {
            var program = new PluginSettings().Items!.Add("Order");
            program.Plugins!.Add("A.First, A", PluginStage.BeforeSave);

            Assert.Throws<ArgumentException>(
                () => program.Plugins!.Add("A.First, A", PluginStage.AfterSave));
        }

        [Fact]
        [DisplayName("Different programs can each declare the same type")]
        public void Plugins_SameTypeUnderDifferentPrograms_Allowed()
        {
            var settings = new PluginSettings();
            settings.Items!.Add("Order").Plugins!.Add("A.Shared, A", PluginStage.BeforeSave);
            settings.Items!.Add("Customer").Plugins!.Add("A.Shared, A", PluginStage.BeforeSave);

            Assert.Equal("A.Shared, A", Assert.Single(settings.GetPluginBindings("Order")).Type);
            Assert.Equal("A.Shared, A", Assert.Single(settings.GetPluginBindings("Customer")).Type);
        }

        [Fact]
        [DisplayName("An XML round-trip keeps the progIds, the chain order and each stage")]
        public void XmlRoundTrip_PreservesProgramsOrderAndStages()
        {
            var settings = new PluginSettings();
            var order = settings.Items!.Add("Order");
            order.Plugins!.Add("A.CreditLimit, A", PluginStage.BeforeSave);
            order.Plugins!.Add("A.Sync, A", PluginStage.AfterSave);
            settings.Items!.Add("Customer").Plugins!.Add("A.Dedupe, A", PluginStage.BeforeSave);

            var restored = XmlCodec.Deserialize<PluginSettings>(XmlCodec.Serialize(settings))!;

            Assert.Equal(s_orderChain, restored.GetPluginBindings("Order"));
            Assert.Equal(s_customerChain, restored.GetPluginBindings("Customer"));
        }

        [Fact]
        [DisplayName("The stage is written as an XML attribute, so a hand-written file shows at a glance which plugin runs at which stage")]
        public void Serialize_WritesStageAsXmlAttribute()
        {
            var settings = new PluginSettings();
            settings.Items!.Add("Order").Plugins!.Add("A.CreditLimit, A", PluginStage.BeforeSave);

            string xml = XmlCodec.Serialize(settings);

            Assert.Contains(@"Type=""A.CreditLimit, A""", xml, StringComparison.Ordinal);
            Assert.Contains(@"Stage=""BeforeSave""", xml, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A hand-written file missing the Stage attribute yields None instead of silently becoming the first stage")]
        public void Deserialize_MissingStageAttribute_YieldsNone()
        {
            // A missing XmlAttribute raises no error; the property just gets the type's default value. The enum's 0 value is
            // therefore reserved for "not declared", so both gates can say "you did not declare a Stage" instead of reporting a
            // stage comparison that makes no sense.
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <PluginSettings xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
                  <Items>
                    <ProgramPluginItem ProgId="Order">
                      <Plugins>
                        <PluginItem Type="A.CreditLimit, A" />
                      </Plugins>
                    </ProgramPluginItem>
                  </Items>
                </PluginSettings>
                """;

            var restored = XmlCodec.Deserialize<PluginSettings>(xml)!;

            Assert.Equal(PluginStage.None, Assert.Single(restored.GetPluginBindings("Order")).Stage);
        }

        [Fact]
        [DisplayName("An empty PluginSettings stays usable after a round-trip and contains no program")]
        public void XmlRoundTrip_Empty_StaysUsable()
        {
            var restored = XmlCodec.Deserialize<PluginSettings>(XmlCodec.Serialize(new PluginSettings()))!;

            Assert.Empty(restored.GetPluginBindings("Order"));
        }
    }
}
