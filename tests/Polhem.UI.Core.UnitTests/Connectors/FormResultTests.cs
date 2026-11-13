using System.ComponentModel;
using System.Data;
using Polhem.UI.Core.Connectors;

namespace Polhem.UI.Core.UnitTests.Connectors
{
    /// <summary>
    /// The result records a connector returns for data and delete calls.
    /// </summary>
    public class FormResultTests
    {
        [Fact]
        [DisplayName("A data result carries its data set and starts with no affected rows")]
        public void FormDataResult_New_HasDataSetAndNoAffectedRows()
        {
            var dataSet = new DataSet("Order");

            var result = new FormDataResult(dataSet);

            Assert.Same(dataSet, result.DataSet);
            Assert.Empty(result.AffectedRows);
        }

        [Fact]
        [DisplayName("A data result can carry the rows each table affected")]
        public void FormDataResult_WithAffectedRows_ExposesCounts()
        {
            var result = new FormDataResult(null);

            var counted = result with { AffectedRows = new Dictionary<string, int> { ["Order"] = 2 } };

            Assert.Null(counted.DataSet);
            Assert.Equal(2, counted.AffectedRows["Order"]);
        }

        [Fact]
        [DisplayName("A delete result carries the number of rows deleted")]
        public void FormDeleteResult_New_HasRowsAffected()
        {
            Assert.Equal(3, new FormDeleteResult(3).RowsAffected);
        }
    }
}
