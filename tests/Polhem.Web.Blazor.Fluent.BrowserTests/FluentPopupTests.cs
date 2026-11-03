using System.ComponentModel;
using Microsoft.Playwright;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Fluent.BrowserTests
{
    /// <summary>
    /// What a lookup does only in a real browser, in a form scope and over a slow link. A Fluent dialog stays inline, and its
    /// input reports its text only when it is left.
    /// </summary>
    public class FluentLookupTests : IClassFixture<FluentLookupFixture>
    {
        private const string Dialog = "fluent-dialog.polhem-lookup-dialog";
        private readonly FluentLookupFixture _fixture;

        public FluentLookupTests(FluentLookupFixture fixture) => _fixture = fixture;

        private async Task<IPage> OpenAsync(bool slow)
        {
            var page = await _fixture.Browser.NewPageAsync();
            await page.GotoAsync((slow ? _fixture.SlowAddress : _fixture.Address) + "/lookup");
            await page.Locator("body[data-interactive]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
            await page.Locator(".polhem-button-edit__search").ClickAsync();
            await Assertions.Expect(page.Locator($"{Dialog} tr.polhem-grid__row")).ToHaveCountAsync(2);
            // The rows are in the markup before the dialog shows, and a key means nothing until it has the focus.
            await page.WaitForFunctionAsync("document.activeElement.tagName === 'FLUENT-DIALOG'");
            return page;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [DisplayName("Choosing a row and OK writes the value and closes the lookup")]
        public async Task SelectThenOk_Writes(bool slow)
        {
            var page = await OpenAsync(slow);

            await page.Locator($"{Dialog} tr.polhem-grid__row").Nth(1).ClickAsync();
            await Assertions.Expect(page.Locator($"{Dialog} tr.polhem-grid__row--selected")).ToHaveCountAsync(1);
            await page.Locator(".polhem-lookup-panel__ok").ClickAsync();

            await Assertions.Expect(page.Locator(Dialog)).ToHaveCountAsync(0);
            await Assertions.Expect(page.Locator("#data")).ToContainTextAsync(LookupTestData.SalesId.ToString());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [DisplayName("A double-click on a row writes the value and closes the lookup")]
        public async Task DoubleClick_Writes(bool slow)
        {
            var page = await OpenAsync(slow);

            await page.Locator($"{Dialog} tr.polhem-grid__row").First.DblClickAsync();

            await Assertions.Expect(page.Locator(Dialog)).ToHaveCountAsync(0);
            await Assertions.Expect(page.Locator("#data")).ToContainTextAsync(LookupTestData.EngineeringId.ToString());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [DisplayName("Typing a search and pressing Enter at once searches for what was typed")]
        public async Task TypeThenEnter_Searches(bool slow)
        {
            var page = await OpenAsync(slow);

            await page.Locator($"{Dialog} .polhem-lookup-panel__search-input input").First.ClickAsync();
            await page.Keyboard.TypeAsync("Sal");
            await page.Keyboard.PressAsync("Enter");

            await Assertions.Expect(page.Locator($"{Dialog} tr.polhem-grid__row")).ToHaveCountAsync(1);
            Assert.Equal("Sal", _fixture.Source.Connector.Searches[^1]);
            await Assertions.Expect(page.Locator(Dialog)).ToHaveCountAsync(1);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [DisplayName("Escape closes the lookup and writes nothing")]
        public async Task Escape_Closes(bool slow)
        {
            var page = await OpenAsync(slow);

            await page.Keyboard.PressAsync("Escape");

            await Assertions.Expect(page.Locator(Dialog)).ToHaveCountAsync(0);
            await Assertions.Expect(page.Locator("#data")).ToHaveTextAsync(Guid.Empty.ToString());
        }

        [BrowserFact]
        [DisplayName("A click outside closes the lookup and writes nothing")]
        public async Task Backdrop_Closes()
        {
            var page = await OpenAsync(false);

            await page.Mouse.ClickAsync(5, 5);

            await Assertions.Expect(page.Locator(Dialog)).ToHaveCountAsync(0);
        }
    }

    /// <summary>
    /// What a select, a check box, a calendar and the row edit dialog do only in a real browser.
    /// </summary>
    public class FluentPopupTests : IClassFixture<FluentSiteFixture>
    {
        private readonly FluentSiteFixture _fixture;

        public FluentPopupTests(FluentSiteFixture fixture) => _fixture = fixture;

        private async Task<IPage> OpenAsync(bool slow)
        {
            var page = await _fixture.Browser.NewPageAsync();
            await page.GotoAsync((slow ? _fixture.SlowAddress : _fixture.Address) + "/select");
            await page.Locator("body[data-interactive]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
            await page.WaitForFunctionAsync("performance.getEntriesByType('resource').some(e => e.name.includes('polhem-grid-editor.js'))");
            return page;
        }

        private static ILocator Data(IPage page) => page.Locator("#data");

        private static ILocator LineCell(IPage page, int column)
            => page.Locator("tr.polhem-grid__row").First.Locator(".polhem-grid-cell__text, .polhem-grid-cell__editor").Nth(column);

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [DisplayName("Picking an option with the mouse writes it")]
        public async Task Form_PickWithMouse(bool slow)
        {
            var page = await OpenAsync(slow);

            await page.Locator("#form-select fluent-dropdown").ClickAsync();
            await page.Locator("#form-select fluent-option", new PageLocatorOptions { HasText = "Beta" }).ClickAsync();

            await Assertions.Expect(Data(page)).ToContainTextAsync("kind=B");
        }

        [BrowserFact]
        [DisplayName("Picking an option with the keyboard writes it")]
        public async Task Form_PickWithKeyboard()
        {
            var page = await OpenAsync(false);

            await page.Locator("#form-select fluent-dropdown button").FocusAsync();
            await page.Keyboard.PressAsync("Space");
            await page.Keyboard.PressAsync("ArrowDown");
            await page.Keyboard.PressAsync("Enter");

            await Assertions.Expect(Data(page)).ToContainTextAsync("kind=B");
        }

        [BrowserFact]
        [DisplayName("A value written from outside shows in the select")]
        public async Task Form_ExternalWriteShows()
        {
            var page = await OpenAsync(false);

            await page.Locator("#write").ClickAsync();

            await Assertions.Expect(Data(page)).ToContainTextAsync("kind=B");
            await Assertions.Expect(page.Locator("#form-select fluent-dropdown")).ToContainTextAsync("Beta");
        }

        [BrowserFact]
        [DisplayName("A check box toggles with the mouse and with Space")]
        public async Task Form_CheckBox()
        {
            var page = await OpenAsync(false);

            await page.Locator("#form-check fluent-checkbox").ClickAsync();
            await Assertions.Expect(Data(page)).ToContainTextAsync("flag=True");
            await page.Locator("#form-check fluent-checkbox").FocusAsync();
            await page.Keyboard.PressAsync("Space");
            await Assertions.Expect(Data(page)).ToContainTextAsync("flag=False");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [DisplayName("Tabbing into a select cell gives it the focus, and the keyboard picks an option")]
        public async Task Cell_TabIntoSelect_KeyboardPicks(bool slow)
        {
            var page = await OpenAsync(slow);

            await LineCell(page, 0).ClickAsync();
            await page.Locator(".polhem-grid-cell__editor input").WaitForAsync();
            await page.Keyboard.PressAsync("Tab");
            await Assertions.Expect(page.Locator(".polhem-grid-cell__editor fluent-dropdown button")).ToBeFocusedAsync();

            await page.Keyboard.PressAsync("Space");
            await page.Keyboard.PressAsync("ArrowDown");
            await page.Keyboard.PressAsync("Enter");

            await Assertions.Expect(Data(page)).ToContainTextAsync("line=Bolt,B,");
        }

        [BrowserFact]
        [DisplayName("Escape with the option list open closes the list and leaves the cell's editor open")]
        public async Task Cell_EscapeWithListOpen_KeepsEditor()
        {
            var page = await OpenAsync(false);

            await LineCell(page, 1).ClickAsync();
            await page.Locator(".polhem-grid-cell__editor fluent-dropdown").ClickAsync();
            await Assertions.Expect(page.Locator(".polhem-grid-cell__editor fluent-option").First).ToBeVisibleAsync();
            await page.Keyboard.PressAsync("Escape");

            await Assertions.Expect(page.Locator(".polhem-grid-cell__editor fluent-dropdown")).ToHaveCountAsync(1);
            await Assertions.Expect(page.Locator(".polhem-grid-cell__editor fluent-option").First).Not.ToBeVisibleAsync();
        }

        [BrowserFact]
        [DisplayName("A date cell's calendar opens, is not clipped by the grid, and a pick writes the date")]
        public async Task Cell_DateCalendarPick()
        {
            var page = await OpenAsync(false);

            await LineCell(page, 2).ClickAsync();
            await page.Locator(".polhem-grid-cell__editor svg").First.ClickAsync();
            await page.Locator("[class*=calendar] >> text=/^18$/").First.ClickAsync(new LocatorClickOptions { Timeout = 5000 });

            await Assertions.Expect(Data(page)).ToContainTextAsync("line=Bolt,A,2026-03-18");
        }

        private async Task<IPage> OpenRowEditAsync(bool slow)
        {
            var page = await OpenAsync(slow);
            await page.Locator("tr.polhem-grid__row").Nth(1).ClickAsync();
            await page.Locator("tr.polhem-grid__row").Nth(1).DblClickAsync();
            await Assertions.Expect(page.Locator("fluent-dialog.polhem-row-edit-dialog .polhem-row-edit-panel")).ToBeVisibleAsync();
            return page;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [DisplayName("The row edit dialog commits typed text on OK clicked right after typing")]
        public async Task RowEdit_TypeThenOk_Commits(bool slow)
        {
            var page = await OpenRowEditAsync(slow);

            await page.Locator("fluent-dialog.polhem-row-edit-dialog .polhem-field-editor--text input").First.ClickAsync();
            await page.Keyboard.PressAsync("ControlOrMeta+a");
            await page.Keyboard.TypeAsync("Washer");
            await page.Locator(".polhem-row-edit-panel__ok").ClickAsync();

            await Assertions.Expect(page.Locator("fluent-dialog.polhem-row-edit-dialog")).ToHaveCountAsync(0);
            await Assertions.Expect(Data(page)).ToContainTextAsync("row=Washer,");
        }

        [BrowserFact]
        [DisplayName("Neither Escape nor a click outside closes the row edit dialog, which holds typed input")]
        public async Task RowEdit_NotDismissable()
        {
            var page = await OpenRowEditAsync(false);

            await page.Keyboard.PressAsync("Escape");
            await page.WaitForTimeoutAsync(500);
            await Assertions.Expect(page.Locator("fluent-dialog.polhem-row-edit-dialog .polhem-row-edit-panel")).ToBeVisibleAsync();

            await page.Mouse.ClickAsync(5, 5);
            await page.WaitForTimeoutAsync(500);
            await Assertions.Expect(page.Locator("fluent-dialog.polhem-row-edit-dialog .polhem-row-edit-panel")).ToBeVisibleAsync();
        }

        [BrowserFact]
        [DisplayName("A calendar opened inside the modal dialog sits on top and takes a pick")]
        public async Task RowEdit_CalendarInsideModal()
        {
            var page = await OpenRowEditAsync(false);

            await page.Locator("fluent-dialog.polhem-row-edit-dialog svg").First.ClickAsync();
            await page.Locator("[class*=calendar] >> text=/^18$/").First.ClickAsync(new LocatorClickOptions { Timeout = 5000 });
            await page.Locator(".polhem-row-edit-panel__ok").ClickAsync();

            await Assertions.Expect(Data(page)).ToContainTextAsync("row=Nut,2026-03-18");
        }

        [BrowserFact]
        [DisplayName("A calendar left open for a while stays open and still takes a pick")]
        public async Task Cell_CalendarSlowPick()
        {
            var page = await OpenAsync(false);

            await LineCell(page, 2).ClickAsync();
            await page.Locator(".polhem-grid-cell__editor svg").First.ClickAsync();
            // Longer than the delay after which leaving the input recreates the picker.
            await page.WaitForTimeoutAsync(1000);
            await page.Locator("[class*=calendar] >> text=/^18$/").First.ClickAsync(new LocatorClickOptions { Timeout = 5000 });

            await Assertions.Expect(Data(page)).ToContainTextAsync("line=Bolt,A,2026-03-18");
        }

        [BrowserFact]
        [DisplayName("Tab stays inside the modal dialog")]
        public async Task RowEdit_TabStaysInside()
        {
            var page = await OpenRowEditAsync(false);

            await page.Locator("fluent-dialog.polhem-row-edit-dialog .polhem-field-editor--text input").First.ClickAsync();
            foreach (var _ in Enumerable.Range(0, 8))
            {
                await page.Keyboard.PressAsync("Tab");
                var inside = await page.EvaluateAsync<bool>("(() => { const a = document.activeElement; return a === document.body || !!a.closest('fluent-dialog'); })()");
                Assert.True(inside);
            }
        }

        [BrowserFact]
        [DisplayName("Leaving a list while its grid is still setting up over a slow link keeps the circuit alive")]
        public async Task SlowLink_LeaveListAtOnce_KeepsTheCircuit()
        {
            var errors = new List<string>();
            var page = await _fixture.Browser.NewPageAsync();
            page.Console += (_, message) =>
            {
                if (message.Type == "error" && message.Text.Contains("circuit", StringComparison.OrdinalIgnoreCase)) errors.Add(message.Text);
            };
            await page.GotoAsync(_fixture.SlowAddress + "/form");
            await page.Locator("body[data-interactive]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
            await Assertions.Expect(page.Locator("tr.polhem-grid__row")).ToHaveCountAsync(2);

            await page.Locator("tr.polhem-grid__row").First.DblClickAsync();
            await Assertions.Expect(page.Locator(".polhem-form-layout")).ToBeVisibleAsync();
            await page.Locator(".polhem-form-page__toolbar fluent-button").First.ClickAsync();

            await Assertions.Expect(page.Locator("tr.polhem-grid__row")).ToHaveCountAsync(2);
            Assert.Empty(errors);
        }

        [BrowserFact]
        [DisplayName("On a phone a wide grid scrolls by itself and the page does not get wider than the screen")]
        public async Task Phone_WideGrid_DoesNotWidenThePage()
        {
            var page = await _fixture.Browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = 300, Height = 700 } });
            await page.GotoAsync(_fixture.Address + "/select");
            await page.Locator("body[data-interactive]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
            await page.Locator("tr.polhem-grid__row").First.WaitForAsync();

            // The page's data line is for the other tests, and it does not wrap.
            await page.Locator("#data").EvaluateAsync("e => e.style.display = 'none'");

            var overflow = await page.EvaluateAsync<int>("document.documentElement.scrollWidth - document.documentElement.clientWidth");

            Assert.Equal(0, overflow);
        }

        [BrowserFact]
        [DisplayName("On a phone every command of the form page stays on the screen")]
        public async Task Phone_Toolbar_StaysOnTheScreen()
        {
            var page = await _fixture.Browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = 300, Height = 700 } });
            await page.GotoAsync(_fixture.Address + "/form");
            await page.Locator("body[data-interactive]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
            await page.Locator(".polhem-form-page__toolbar fluent-button").First.WaitForAsync();

            var rightmost = await page.EvaluateAsync<double>("Math.max(...[...document.querySelectorAll('.polhem-form-page__toolbar fluent-button')].map(b => b.getBoundingClientRect().right))");

            Assert.True(rightmost <= 300, $"A command ends at {rightmost}, past the screen.");
        }
    }
}
