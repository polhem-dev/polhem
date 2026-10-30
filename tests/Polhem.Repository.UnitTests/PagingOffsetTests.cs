using System.ComponentModel;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// <see cref="PagingOffset"/> turns a page into the rows to skip without overflowing.
    /// </summary>
    public class PagingOffsetTests
    {
        [Theory]
        [InlineData(1, 50, 0)]
        [InlineData(3, 1000, 2000)]
        [DisplayName("Compute returns the rows before the page")]
        public void Compute_OrdinaryPage_ReturnsRowsBeforePage(int page, int pageSize, int expected)
        {
            Assert.Equal(expected, PagingOffset.Compute(page, pageSize));
        }

        [Theory]
        [InlineData(2_147_484, 1000)]
        [InlineData(4_294_969, 1000)]
        [InlineData(int.MaxValue, 1000)]
        [InlineData(int.MaxValue, 1)]
        [DisplayName("Compute leaves room for the page and its probe row when the offset does not fit an int")]
        public void Compute_OffsetBeyondInt_LeavesRoomForProbeRow(int page, int pageSize)
        {
            Assert.Equal(int.MaxValue - pageSize - 1, PagingOffset.Compute(page, pageSize));
        }
    }
}
