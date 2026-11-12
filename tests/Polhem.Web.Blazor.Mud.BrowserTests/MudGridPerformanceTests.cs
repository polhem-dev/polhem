using Polhem.Web.Blazor.BrowserTests;
using Xunit.Abstractions;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// Runs the shared first load test on the MudBlazor UI package. Measured on 2026-11-01 with the same page and method:
    /// the MudBlazor grid sends 1.76 MB and holds 30 to 36 MB, the HTML grid 1.53 MB and 53 to 59 MB. The maintainer
    /// accepted the extra traffic for the memory.
    /// </summary>
    public class MudGridPerformanceTests : GridPerformanceTests, IClassFixture<MudSiteFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MudGridPerformanceTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        /// <param name="output">Where the measurements go.</param>
        public MudGridPerformanceTests(MudSiteFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }

        /// <inheritdoc />
        protected override long FirstLoadLimitBytes => 1_900_000;

        /// <inheritdoc />
        /// <remarks>Measured on 2026-11-12: a commit sent 15.8 KB more than a move, about 79 bytes a row.</remarks>
        protected override long CommitAllowanceBytes => 20_000;
    }
}
