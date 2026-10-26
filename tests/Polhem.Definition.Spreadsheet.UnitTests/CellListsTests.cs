using System.ComponentModel;
using Polhem.Definition.Forms;

namespace Polhem.Definition.Spreadsheet.UnitTests
{
    /// <summary>
    /// How <see cref="CellLists"/> writes field mappings into one cell and reads them back.
    /// </summary>
    public class CellListsTests
    {
        [Fact]
        [DisplayName("A lookup-only mapping is written with a trailing (lookup) and read back as lookup-only")]
        public void Mappings_LookupOnly_RoundTrip()
        {
            FieldMappingCollection mappings =
            [
                new FieldMapping("sys_name", "ref_customer_name"),
                new FieldMapping("sales_rowid", "sales_rowid") { LookupOnly = true },
            ];

            var text = CellLists.FormatMappings(mappings);
            FieldMappingCollection restored = [];
            var error = CellLists.ParseMappings(text, restored);

            Assert.Equal("sys_name=ref_customer_name; sales_rowid=sales_rowid (lookup)", text);
            Assert.Null(error);
            Assert.Equal([false, true], restored.Select(m => m.LookupOnly));
            Assert.Equal("sales_rowid", restored[1].DestinationField);
        }

        [Fact]
        [DisplayName("The (lookup) marker is read regardless of case and spacing")]
        public void ParseMappings_MarkerCaseAndSpacing_AreTolerated()
        {
            FieldMappingCollection restored = [];

            Assert.Null(CellLists.ParseMappings("sales_rowid = sales_rowid(LOOKUP)", restored));

            var mapping = Assert.Single(restored);
            Assert.True(mapping.LookupOnly);
            Assert.Equal("sales_rowid", mapping.DestinationField);
        }

        [Fact]
        [DisplayName("A marker without a pair is refused")]
        public void ParseMappings_MarkerOnly_ReturnsError()
        {
            Assert.NotNull(CellLists.ParseMappings("(lookup)", []));
        }
    }
}
