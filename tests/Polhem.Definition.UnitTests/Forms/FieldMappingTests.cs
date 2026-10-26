using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Forms;

namespace Polhem.Definition.UnitTests.Forms
{
    public class FieldMappingTests
    {
        [Fact]
        [DisplayName("ToString returns the \"{SourceField} -> {DestinationField}\" format")]
        public void ToString_ReturnsFormattedString()
        {
            var mapping = new FieldMapping("dept_name", "name");

            Assert.Equal("dept_name -> name", mapping.ToString());
        }

        [Fact]
        [DisplayName("ToString marks a lookup-only mapping")]
        public void ToString_LookupOnly_AddsMarker()
        {
            var mapping = new FieldMapping("dept_rowid", "dept_rowid") { LookupOnly = true };

            Assert.Equal("dept_rowid -> dept_rowid (lookup)", mapping.ToString());
        }

        [Fact]
        [DisplayName("Clone copies LookupOnly")]
        public void Clone_LookupOnly_IsCopied()
        {
            var copy = new FieldMapping("dept_rowid", "dept_rowid") { LookupOnly = true }.Clone();

            Assert.True(copy.LookupOnly);
        }

        [Fact]
        [DisplayName("XML omits LookupOnly while it is false")]
        public void Serialize_LookupOnlyFalse_IsOmitted()
        {
            var xml = XmlCodec.Serialize(BuildField(lookupOnly: false));

            Assert.DoesNotContain("LookupOnly", xml);
        }

        [Fact]
        [DisplayName("XML writes LookupOnly when true and reads it back")]
        public void Serialize_LookupOnlyTrue_RoundTrips()
        {
            var xml = XmlCodec.Serialize(BuildField(lookupOnly: true));
            var restored = XmlCodec.Deserialize<FormField>(xml)!;

            Assert.Contains("LookupOnly=\"true\"", xml);
            Assert.True(restored.RelationFieldMappings![0].LookupOnly);
        }

        private static FormField BuildField(bool lookupOnly)
        {
            var field = new FormField("emp_rowid", "Employee", FieldDbType.Guid) { RelationProgId = "Employee" };
            field.RelationFieldMappings!.Add(new FieldMapping("dept_rowid", "dept_rowid") { LookupOnly = lookupOnly });
            return field;
        }
    }
}
