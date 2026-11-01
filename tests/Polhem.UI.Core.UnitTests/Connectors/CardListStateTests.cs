using System.ComponentModel;
using Polhem.Definition.Paging;
using Polhem.UI.Core.Connectors;

namespace Polhem.UI.Core.UnitTests.Connectors
{
    public class CardListStateTests
    {
        private readonly List<string> _rows = [];

        private CardListState<string> CreateState() => new(_rows, row => row);

        [Fact]
        [DisplayName("A later page extends the cards, and page 1 replaces them")]
        public void ShowPage_PageOne_Replaces()
        {
            var state = CreateState();

            state.ShowPage(1, ["a", "b"]);
            state.ShowPage(2, ["c"]);
            Assert.Equal(["a", "b", "c"], _rows);

            state.ShowPage(1, ["c"]);
            Assert.Equal(["c"], _rows);
        }

        [Fact]
        [DisplayName("A row that arrives on the next page again appears once")]
        public void ShowPage_RepeatedRow_AppearsOnce()
        {
            var state = CreateState();

            state.ShowPage(1, ["a", "b"]);
            state.ShowPage(2, ["B", "c"]);

            Assert.Equal(["a", "b", "c"], _rows);
        }

        [Fact]
        [DisplayName("Page 1 forgets the rows it replaced, so they can return")]
        public void ShowPage_PageOne_ForgetsIds()
        {
            var state = CreateState();

            state.ShowPage(1, ["a"]);
            state.ShowPage(1, ["b"]);
            state.ShowPage(2, ["a"]);

            Assert.Equal(["b", "a"], _rows);
        }

        [Fact]
        [DisplayName("The cards stop at the limit, and then no more pages are read")]
        public void ShowPage_AtLimit_Stops()
        {
            var state = CreateState();
            var paging = new ListPagingState();
            paging.Apply(1, new PagingInfo { Page = 1, PageSize = 50, HasMore = true });
            Assert.True(state.CanLoadMore(paging));

            state.ShowPage(1, Enumerable.Range(0, CardListState<string>.MaxRows + 10).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)));

            Assert.Equal(CardListState<string>.MaxRows, _rows.Count);
            Assert.True(state.IsAtLimit);
            Assert.False(state.CanLoadMore(paging));
        }

        [Fact]
        [DisplayName("Without a next page no more pages are read")]
        public void CanLoadMore_NoNextPage_IsFalse()
        {
            Assert.False(CreateState().CanLoadMore(new ListPagingState()));
        }
    }
}
