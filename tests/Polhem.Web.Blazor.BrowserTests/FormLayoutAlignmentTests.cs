using System.ComponentModel;
using Microsoft.Playwright;

namespace Polhem.Web.Blazor.BrowserTests
{
    /// <summary>
    /// How the editors of one form row line up. A read-only editor draws only its bottom line, and a UI package's
    /// stylesheet decides where that line sits, so only a browser shows whether it meets an editable neighbour's edge.
    /// </summary>
    public abstract class FormLayoutAlignmentTests
    {
        private readonly BrowserSiteFixture _fixture;

        /// <summary>
        /// Initializes a new instance of <see cref="FormLayoutAlignmentTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        protected FormLayoutAlignmentTests(BrowserSiteFixture fixture) => _fixture = fixture;

        /// <summary>Gets the selector of a form page command button that can be clicked.</summary>
        protected virtual string EnabledCommandButton => ".polhem-form-page__toolbar button:enabled";

        // The editor is the element after the field's label; its box ends where its bottom line is drawn.
        private const string EditorBottoms = """
            () => ['qty', 'amount'].map(name => {
                const label = document.querySelector(`label[for="polhem-form-${name}"]`);
                const editor = label && [...label.parentElement.children].find(e => e !== label);
                return editor ? editor.getBoundingClientRect().bottom : null;
            })
            """;

        [BrowserFact]
        [DisplayName("A read-only editor's bottom line lines up with the editable editor beside it")]
        public async Task ReadOnlyEditor_LinesUpWithEditableNeighbour()
        {
            var page = await _fixture.Browser.NewPageAsync();
            await page.GotoAsync(_fixture.Address + "/form");
            await page.Locator("body[data-interactive]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
            await page.Locator(EnabledCommandButton, new PageLocatorOptions { HasText = "New" }).ClickAsync();
            await Assertions.Expect(page.Locator("label[for=\"polhem-form-amount\"]")).ToBeVisibleAsync();

            var bottoms = await (await page.WaitForFunctionAsync(
                $"() => {{ const b = ({EditorBottoms})(); return b.every(v => v !== null) ? b : null; }}")).JsonValueAsync<double[]>();

            Assert.Equal(bottoms[0], bottoms[1], 0.5);
        }
    }
}
