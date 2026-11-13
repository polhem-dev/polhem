using System.ComponentModel;
using Polhem.Business.Form;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// The maps of a business result keep matching field and table names in any case when they are replaced.
    /// </summary>
    public class ResultMapComparerTests
    {
        [Fact]
        [DisplayName("FieldValueChangeResult keeps its errors and warnings case-insensitive when they are replaced")]
        public void FieldValueChangeResult_ReplacedMaps_IgnoreCase()
        {
            var result = new FieldValueChangeResult
            {
                Errors = new Dictionary<string, string>(StringComparer.Ordinal) { ["qty"] = "required" },
                Warnings = new Dictionary<string, string>(StringComparer.Ordinal) { ["price"] = "high" },
            };

            Assert.Equal("required", result.Errors["QTY"]);
            Assert.Equal("high", result.Warnings["PRICE"]);
        }

        [Fact]
        [DisplayName("SaveResult keeps its affected rows case-insensitive when they are replaced")]
        public void SaveResult_ReplacedMap_IgnoresCase()
        {
            var result = new SaveResult
            {
                AffectedRows = new Dictionary<string, int>(StringComparer.Ordinal) { ["ft_order"] = 2 },
            };

            Assert.Equal(2, result.AffectedRows["FT_ORDER"]);
        }
    }
}
