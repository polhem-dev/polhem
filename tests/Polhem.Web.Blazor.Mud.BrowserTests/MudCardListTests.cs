using Polhem.Web.Blazor.BrowserTests;
using Xunit.Abstractions;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// Runs the shared card list tests on the MudBlazor UI package.
    /// </summary>
    public class MudCardListTests : CardListTests, IClassFixture<MudCardListFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MudCardListTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        /// <param name="output">Where the measurements go.</param>
        public MudCardListTests(MudCardListFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }
}
