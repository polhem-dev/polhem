using System.ComponentModel;
using Polhem.Definition.Paging;
using Polhem.UI.Core.Connectors;

namespace Polhem.UI.Core.UnitTests.Connectors
{
    /// <summary>
    /// <see cref="ListPagingState"/> tracks the page on screen and what the next request asks for.
    /// </summary>
    public class ListPagingStateTests
    {
        [Fact]
        [DisplayName("CreateOptions counts the rows only while the count is not known")]
        public void CreateOptions_CountKnown_DoesNotCountAgain()
        {
            var state = new ListPagingState();
            Assert.True(state.CreateOptions(1).IncludeTotalCount);

            state.Apply(1, new PagingInfo { TotalCount = 120, HasMore = true });

            var options = state.CreateOptions(2);
            Assert.False(options.IncludeTotalCount);
            Assert.Equal(2, options.Page);
            Assert.Equal(ListPagingState.DefaultPageSize, options.PageSize);
        }

        [Fact]
        [DisplayName("Apply keeps a known count when the response carries none")]
        public void Apply_ResponseWithoutCount_KeepsKnownCount()
        {
            var state = new ListPagingState();
            state.Apply(1, new PagingInfo { TotalCount = 120, HasMore = true });

            state.Apply(2, new PagingInfo { HasMore = true });

            Assert.Equal(120, state.TotalCount);
            Assert.Equal(3, state.TotalPages);
            Assert.Equal(2, state.Page);
            Assert.True(state.CanGoPrevious);
            Assert.True(state.CanGoNext);
        }

        [Fact]
        [DisplayName("Apply without paging metadata reports no further page")]
        public void Apply_NoPaging_HasNoMore()
        {
            var state = new ListPagingState();

            state.Apply(1, null);

            Assert.False(state.CanGoNext);
            Assert.Null(state.TotalPages);
        }

        [Fact]
        [DisplayName("SetPageSize returns to the first page and counts again")]
        public void SetPageSize_ReturnsToFirstPageAndForgetsCount()
        {
            var state = new ListPagingState();
            state.Apply(3, new PagingInfo { TotalCount = 120 });

            state.SetPageSize(100);

            Assert.Equal(1, state.Page);
            Assert.Equal(100, state.PageSize);
            Assert.Null(state.TotalCount);
        }

        [Fact]
        [DisplayName("SetPageSize refuses a page size below 1")]
        public void SetPageSize_BelowOne_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ListPagingState().SetPageSize(0));
        }

        [Fact]
        [DisplayName("IsCurrent is false for a request a newer one replaced")]
        public void IsCurrent_NewerRequest_ReturnsFalse()
        {
            var state = new ListPagingState();
            var first = state.BeginRequest();
            var second = state.BeginRequest();

            Assert.False(state.IsCurrent(first));
            Assert.True(state.IsCurrent(second));
        }

        [Fact]
        [DisplayName("An empty page moves back to the last page the count allows")]
        public void TryStepBack_EmptyPageWithCount_MovesToLastPage()
        {
            var state = new ListPagingState();
            state.Apply(5, new PagingInfo { TotalCount = 60 });

            Assert.True(state.TryStepBackFromEmptyPage(0));

            Assert.Equal(2, state.Page);
        }

        [Fact]
        [DisplayName("An empty page moves back one page when the count is not known")]
        public void TryStepBack_EmptyPageWithoutCount_MovesBackOne()
        {
            var state = new ListPagingState();
            state.Apply(5, new PagingInfo());

            Assert.True(state.TryStepBackFromEmptyPage(0));

            Assert.Equal(4, state.Page);
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(2, 1)]
        [DisplayName("The first page and a page with rows stay where they are")]
        public void TryStepBack_FirstPageOrRows_DoesNotMove(int page, int rowCount)
        {
            var state = new ListPagingState();
            state.Apply(page, new PagingInfo());

            Assert.False(state.TryStepBackFromEmptyPage(rowCount));

            Assert.Equal(page, state.Page);
        }
    }
}
