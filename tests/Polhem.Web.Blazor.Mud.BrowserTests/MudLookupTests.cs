using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// Runs the shared lookup tests on the MudBlazor package, whose dialog renders in the dialog provider.
    /// </summary>
    public class MudLookupTests : LookupTests, IClassFixture<MudLookupFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MudLookupTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public MudLookupTests(MudLookupFixture fixture) : base(fixture, fixture.Source) { }

        /// <inheritdoc />
        protected override string Dialog => ".mud-dialog.polhem-lookup-dialog";

        /// <inheritdoc />
        protected override string LookupButton => ".polhem-field-editor--button .mud-input-adornment button";
    }
}
